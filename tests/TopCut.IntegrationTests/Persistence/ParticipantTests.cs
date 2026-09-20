using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The roster: signing players in at the counter, the waitlist filling up and emptying again, and
/// dropping someone. Against real SQL Server, because the waitlist is a queue whose positions have
/// to survive being written down and read back.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ParticipantTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Players_are_signed_up_by_name_alone()
    {
        var (store, tournament) = await NewTournament(capacity: 8);

        Assert.True((await Players(store).RegisterAsync(tournament, "Ana")).Succeeded);
        Assert.True((await Players(store).RegisterAsync(tournament, "Bruno")).Succeeded);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal(["Ana", "Bruno"], roster.Registered.Select(player => player.Name));

        // Nobody here has an account: they walked in and the store typed their name.
        Assert.All(roster.Registered, player => Assert.False(player.HasAccount));
    }

    [Fact]
    public async Task A_name_with_stray_spaces_is_tidied_up_rather_than_refused()
    {
        var (store, tournament) = await NewTournament();

        Assert.True((await Players(store).RegisterAsync(tournament, "  Ana  ")).Succeeded);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal("Ana", roster.Registered.Single().Name);
    }

    [Fact]
    public async Task An_empty_name_is_refused()
    {
        var (store, tournament) = await NewTournament();

        var outcome = await Players(store).RegisterAsync(tournament, "   ");

        Assert.False(outcome.Succeeded);
        Assert.Contains("necesita un nombre", string.Join(" ", outcome.Errors));
        Assert.Empty((await Queries(store).RosterAsync(tournament)).Registered);
    }

    [Fact]
    public async Task Two_people_can_share_a_name_because_walk_ins_have_nothing_else_to_tell_them_apart()
    {
        var (store, tournament) = await NewTournament();

        Assert.True((await Players(store).RegisterAsync(tournament, "Ana")).Succeeded);
        Assert.True((await Players(store).RegisterAsync(tournament, "Ana")).Succeeded);

        Assert.Equal(2, (await Queries(store).RosterAsync(tournament)).Registered.Count);
    }

    // --- Waitlist -------------------------------------------------------------------------------

    [Fact]
    public async Task Once_the_seats_run_out_the_next_arrivals_queue_in_order()
    {
        var (store, tournament) = await NewTournament(capacity: 2);

        await Players(store).RegisterAsync(tournament, "Ana");
        await Players(store).RegisterAsync(tournament, "Bruno");

        var third = await Players(store).RegisterAsync(tournament, "Carla");
        var fourth = await Players(store).RegisterAsync(tournament, "Diego");

        Assert.True(third.Waitlisted);
        Assert.Equal(1, third.WaitlistPosition);
        Assert.True(fourth.Waitlisted);
        Assert.Equal(2, fourth.WaitlistPosition);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal(2, roster.Registered.Count);
        Assert.Equal(["Carla", "Diego"], roster.Waitlisted.Select(player => player.Name));
    }

    [Fact]
    public async Task Dropping_someone_before_the_start_frees_the_seat_and_the_first_in_the_queue_takes_it()
    {
        var (store, tournament) = await NewTournament(capacity: 2);

        await Players(store).RegisterAsync(tournament, "Ana");
        await Players(store).RegisterAsync(tournament, "Bruno");
        await Players(store).RegisterAsync(tournament, "Carla");
        await Players(store).RegisterAsync(tournament, "Diego");

        var ana = (await Queries(store).RosterAsync(tournament)).Registered.Single(p => p.Name == "Ana");

        var dropped = await Players(store).DropAsync(tournament, ana.Id);

        Assert.True(dropped.Succeeded);

        // The organiser is told who moved up, because that player is waiting to hear it.
        Assert.Equal("Carla", dropped.PromotedName);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal(["Bruno", "Carla"], roster.Registered.Select(player => player.Name));
        Assert.Equal("Diego", roster.Waitlisted.Single().Name);

        // And Diego moved up the queue rather than staying at number two behind nobody.
        Assert.Equal(1, roster.Waitlisted.Single().WaitlistPosition);
    }

    [Fact]
    public async Task Dropping_someone_who_was_only_waiting_promotes_nobody()
    {
        var (store, tournament) = await NewTournament(capacity: 2);

        await Players(store).RegisterAsync(tournament, "Ana");
        await Players(store).RegisterAsync(tournament, "Bruno");
        await Players(store).RegisterAsync(tournament, "Carla");

        var carla = (await Queries(store).RosterAsync(tournament)).Waitlisted.Single();

        var dropped = await Players(store).DropAsync(tournament, carla.Id);

        Assert.True(dropped.Succeeded);
        Assert.Null(dropped.PromotedName);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal(2, roster.Registered.Count);
        Assert.Empty(roster.Waitlisted);
    }

    // --- Mid-tournament ---------------------------------------------------------------------------

    [Fact]
    public async Task Dropping_mid_tournament_takes_them_out_of_pairings_without_freeing_a_seat()
    {
        var (store, tournament) = await NewTournament(capacity: 4);

        foreach (var name in new[] { "Ana", "Bruno", "Carla", "Diego", "Elena" })
        {
            await Players(store).RegisterAsync(tournament, name);
        }

        await Setup(store).PublishAsync(tournament);
        await StartFirstRound(store, tournament);

        var ana = (await Queries(store).RosterAsync(tournament)).Registered.Single(p => p.Name == "Ana");
        var dropped = await Players(store).DropAsync(tournament, ana.Id);

        Assert.True(dropped.Succeeded, string.Join(" ", dropped.Errors));

        // Elena stays on the waitlist: a tournament under way does not hand out the empty chair,
        // because she would be a round behind everyone else.
        Assert.Null(dropped.PromotedName);

        var roster = await Queries(store).RosterAsync(tournament);

        Assert.Equal("Ana", roster.Dropped.Single().Name);
        Assert.Equal("Elena", roster.Waitlisted.Single().Name);
    }

    [Fact]
    public async Task A_tournament_under_way_takes_no_new_sign_ups()
    {
        var (store, tournament) = await NewTournament(capacity: 4);

        foreach (var name in new[] { "Ana", "Bruno" })
        {
            await Players(store).RegisterAsync(tournament, name);
        }

        await Setup(store).PublishAsync(tournament);
        await StartFirstRound(store, tournament);

        var late = await Players(store).RegisterAsync(tournament, "Tarde");

        Assert.False(late.Succeeded);
        Assert.Contains("ya no admite inscripciones", string.Join(" ", late.Errors));
    }

    [Fact]
    public async Task Nobody_can_be_dropped_twice()
    {
        var (store, tournament) = await NewTournament();

        await Players(store).RegisterAsync(tournament, "Ana");
        var ana = (await Queries(store).RosterAsync(tournament)).Registered.Single();

        Assert.True((await Players(store).DropAsync(tournament, ana.Id)).Succeeded);

        var again = await Players(store).DropAsync(tournament, ana.Id);

        Assert.False(again.Succeeded);
        Assert.Contains("ya estaba dado de baja", string.Join(" ", again.Errors));
    }

    // --- Renaming ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_misspelt_name_can_be_corrected()
    {
        var (store, tournament) = await NewTournament();

        await Players(store).RegisterAsync(tournament, "Anna");
        var player = (await Queries(store).RosterAsync(tournament)).Registered.Single();

        Assert.True((await Players(store).RenameAsync(tournament, player.Id, "Ana")).Succeeded);

        Assert.Equal("Ana", (await Queries(store).RosterAsync(tournament)).Registered.Single().Name);
    }

    [Fact]
    public async Task Renaming_somebody_who_is_not_in_this_tournament_is_refused()
    {
        var (store, tournament) = await NewTournament();

        var result = await Players(store).RenameAsync(tournament, Guid.CreateVersion7(), "Ana");

        Assert.False(result.Succeeded);
        Assert.Contains("no esta en el torneo", string.Join(" ", result.Errors));
    }

    // --- Multi-tenancy ---------------------------------------------------------------------------------

    [Fact]
    public async Task Another_stores_roster_is_not_readable_or_editable()
    {
        var (owner, tournament) = await NewTournament();
        await Players(owner).RegisterAsync(tournament, "Ana");
        var ana = (await Queries(owner).RosterAsync(tournament)).Registered.Single();

        var intruder = await NewApprovedStore();

        Assert.Equal(0, (await Queries(intruder).RosterAsync(tournament)).Count);

        var refused = await Players(intruder).DropAsync(tournament, ana.Id);

        Assert.False(refused.Succeeded);
        Assert.Contains("No existe ese torneo", string.Join(" ", refused.Errors));

        // And the owner's roster is untouched by the attempt.
        Assert.Single((await Queries(owner).RosterAsync(tournament)).Registered);
    }

    // --- Harness -------------------------------------------------------------------------------------------

    private ParticipantService Players(Guid storeId) =>
        new(new TournamentRepository(fixture.CreateContextFactory(storeId)));

    private TournamentSetupService Setup(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        new TcgCatalog(fixture.CreateContextFactory(storeId)),
        new StoreScope(storeId));

    private ITournamentQueries Queries(Guid storeId) =>
        new TournamentQueries(fixture.CreateContextFactory(storeId), TimeProvider.System);

    private Task<OperationResult> StartFirstRound(Guid storeId, Guid tournamentId) =>
        new TournamentService(
            new TournamentRepository(fixture.CreateContextFactory(storeId)),
            TimeProvider.System)
            .StartNextRoundAsync(tournamentId);

    private async Task<(Guid StoreId, Guid TournamentId)> NewTournament(int capacity = 16)
    {
        var storeId = await NewApprovedStore();

        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        var magic = (await db.Tcgs.SingleAsync(tcg => tcg.Code == SeedCatalog.MagicCode)).Id;

        var created = await Setup(storeId).CreateAsync(new TournamentDraft(
            magic,
            "Torneo de mostrador",
            DateTimeOffset.UtcNow.AddDays(3),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            capacity,
            SwissRounds: 3,
            TopCutSize: null));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return (storeId, created.TournamentId!.Value);
    }

    private async Task<Guid> NewApprovedStore()
    {
        await using var db = fixture.CreateContext();

        var store = new Store(
            $"Tienda {Guid.CreateVersion7():N}",
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));
        store.Approve();

        db.Stores.Add(store);
        await db.SaveChangesAsync();

        return store.Id;
    }

    private sealed class StoreScope(Guid storeId) : ITenantContext
    {
        public Guid? TenantId { get; } = storeId;
    }
}
