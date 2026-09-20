using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// Creating, editing and publishing a tournament, against real SQL Server. These cover the path the
/// store's screens take in milestone 3b, including the two things that are easy to get wrong and
/// impossible to spot by looking: that a refused change saves nothing, and that one store's list
/// never contains another store's tournaments.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class TournamentSetupTests(SqlServerFixture fixture)
{
    // --- Creating -----------------------------------------------------------------------------

    [Fact]
    public async Task A_created_tournament_starts_as_a_draft_with_the_games_own_rules()
    {
        var store = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);

        var created = await Setup(store).CreateAsync(Draft(magic, capacity: 16, swissRounds: 5, topCut: 4));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));

        var details = await Queries(store).DetailsAsync(created.TournamentId!.Value);

        Assert.NotNull(details);
        Assert.Equal(TournamentStatus.Draft, details!.Status);
        Assert.Equal(5, details.SwissRounds);
        Assert.Equal(4, details.TopCutSize);

        // Copied from the Magic ruleset, not invented by the form.
        Assert.Equal(MatchFormat.Bo3, details.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(50), details.SwissPhase.RoundLength);
        Assert.Contains("Magic Tournament Rules", details.RulesVersion);
    }

    [Fact]
    public async Task A_store_can_override_the_games_timings_for_one_tournament()
    {
        var store = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);

        var created = await Setup(store).CreateAsync(
            Draft(magic),
            new TournamentRules(
                new PhaseSettings(MatchFormat.Bo1, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(3)),
                new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(75), TimeSpan.Zero),
                TimeExpiryPolicy.AlwaysDraw));

        var details = await Queries(store).DetailsAsync(created.TournamentId!.Value);

        Assert.Equal(MatchFormat.Bo1, details!.SwissPhase.Format);
        Assert.Equal(TimeSpan.FromMinutes(30), details.SwissPhase.RoundLength);
        Assert.Equal(TimeSpan.FromMinutes(3), details.SwissPhase.ExtraTime);
        Assert.Equal(TimeExpiryPolicy.AlwaysDraw, details.TimeExpiry);
    }

    [Fact]
    public async Task A_tournament_cannot_be_created_for_a_game_that_is_switched_off()
    {
        var store = await NewApprovedStore();

        var created = await Setup(store).CreateAsync(Draft(tcgId: 9999));

        Assert.False(created.Succeeded);
        Assert.Null(created.TournamentId);
        Assert.Contains("no esta disponible", string.Join(" ", created.Errors));
    }

    [Fact]
    public async Task A_top_cut_that_is_not_a_power_of_two_is_refused_and_nothing_is_created()
    {
        var store = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);

        var created = await Setup(store).CreateAsync(Draft(magic, topCut: 6));

        Assert.False(created.Succeeded);
        Assert.Contains("potencia de dos", string.Join(" ", created.Errors));
        Assert.Empty(await Queries(store).ForCurrentStoreAsync());
    }

    // --- Editing -------------------------------------------------------------------------------

    [Fact]
    public async Task Editing_a_draft_changes_its_details_and_its_rules_together()
    {
        var store = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);
        var id = await NewTournament(store, magic);

        var updated = await Setup(store).UpdateAsync(
            id,
            Draft(magic, capacity: 24, swissRounds: 5, topCut: 8) with { Name = "Liga de invierno", City = "Alicante" },
            new TournamentRules(
                new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(55), TimeSpan.Zero),
                new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(90), TimeSpan.Zero),
                TimeExpiryPolicy.LeaderWins));

        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors));

        var details = await Queries(store).DetailsAsync(id);

        Assert.Equal("Liga de invierno", details!.Name);
        Assert.Equal("Alicante", details.Venue.City);
        Assert.Equal(24, details.Capacity);
        Assert.Equal(8, details.TopCutSize);
        Assert.Equal(TimeSpan.FromMinutes(55), details.SwissPhase.RoundLength);
    }

    [Fact]
    public async Task A_refused_edit_leaves_the_tournament_exactly_as_it_was()
    {
        var store = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);
        var id = await NewTournament(store, magic, capacity: 8);

        await RegisterPlayers(store, id, 6);

        // The name would have gone through; the capacity is what breaks the rule. Because the whole
        // edit is one unit of work, neither is saved — which is the point of this test.
        var refused = await Setup(store).UpdateAsync(
            id,
            Draft(magic, capacity: 4) with { Name = "Nombre nuevo" });

        Assert.False(refused.Succeeded);
        Assert.Contains("menos plazas que inscritos", string.Join(" ", refused.Errors));

        var details = await Queries(store).DetailsAsync(id);

        Assert.Equal("Torneo de prueba", details!.Name);
        Assert.Equal(8, details.Capacity);
    }

    // --- Lifecycle -------------------------------------------------------------------------------

    [Fact]
    public async Task A_draft_is_published_and_can_go_back_to_being_a_draft()
    {
        var store = await NewApprovedStore();
        var id = await NewTournament(store, await GameId(SeedCatalog.MagicCode));

        Assert.True((await Setup(store).PublishAsync(id)).Succeeded);
        Assert.Equal(TournamentStatus.Published, (await Queries(store).DetailsAsync(id))!.Status);

        Assert.True((await Setup(store).UnpublishAsync(id)).Succeeded);
        Assert.Equal(TournamentStatus.Draft, (await Queries(store).DetailsAsync(id))!.Status);
    }

    [Fact]
    public async Task A_cancelled_tournament_cannot_be_cancelled_twice()
    {
        var store = await NewApprovedStore();
        var id = await NewTournament(store, await GameId(SeedCatalog.MagicCode));

        Assert.True((await Setup(store).CancelAsync(id)).Succeeded);

        var again = await Setup(store).CancelAsync(id);

        Assert.False(again.Succeeded);
        Assert.Contains("ya estaba cerrado", string.Join(" ", again.Errors));
    }

    // --- Multi-tenancy ----------------------------------------------------------------------------

    [Fact]
    public async Task A_store_only_lists_its_own_tournaments()
    {
        var mine = await NewApprovedStore();
        var theirs = await NewApprovedStore();
        var magic = await GameId(SeedCatalog.MagicCode);

        await NewTournament(mine, magic);
        await NewTournament(mine, magic);
        await NewTournament(theirs, magic);

        Assert.Equal(2, (await Queries(mine).ForCurrentStoreAsync()).Count);
        Assert.Single(await Queries(theirs).ForCurrentStoreAsync());
    }

    [Fact]
    public async Task Another_stores_tournament_is_indistinguishable_from_one_that_does_not_exist()
    {
        var mine = await NewApprovedStore();
        var theirs = await NewApprovedStore();
        var id = await NewTournament(theirs, await GameId(SeedCatalog.MagicCode));

        Assert.Null(await Queries(mine).DetailsAsync(id));
        Assert.Null(await Queries(mine).DetailsAsync(Guid.CreateVersion7()));

        // And it cannot be edited by guessing its id either.
        var refused = await Setup(mine).PublishAsync(id);

        Assert.False(refused.Succeeded);
        Assert.Contains("No existe ese torneo", string.Join(" ", refused.Errors));
    }

    [Fact]
    public async Task The_list_counts_who_is_in_and_who_is_waiting()
    {
        var store = await NewApprovedStore();
        var id = await NewTournament(store, await GameId(SeedCatalog.MagicCode), capacity: 4);

        await RegisterPlayers(store, id, 6);

        var row = Assert.Single(await Queries(store).ForCurrentStoreAsync());

        Assert.Equal(4, row.Registered);
        Assert.Equal(2, row.Waitlisted);
        Assert.True(row.IsFull);
        Assert.Equal("Magic: The Gathering", row.TcgName);
    }

    // --- Harness ------------------------------------------------------------------------------------

    private TournamentSetupService Setup(Guid storeId) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        new TcgCatalog(fixture.CreateContextFactory(storeId)),
        new StoreScope(storeId));

    private ITournamentQueries Queries(Guid storeId) =>
        new TournamentQueries(fixture.CreateContextFactory(storeId), TimeProvider.System);

    private static TournamentDraft Draft(
        int tcgId,
        int capacity = 16,
        int swissRounds = 4,
        int? topCut = null) =>
        new(
            tcgId,
            "Torneo de prueba",
            DateTimeOffset.UtcNow.AddDays(7),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            capacity,
            swissRounds,
            topCut);

    private async Task<int> GameId(string code)
    {
        await using var db = fixture.CreateContext();

        // Idempotent, and asked for here rather than relying on another test class having run first.
        await CatalogSeeder.SeedAsync(db);

        return (await db.Tcgs.SingleAsync(tcg => tcg.Code == code)).Id;
    }

    private async Task<Guid> NewTournament(Guid storeId, int tcgId, int capacity = 16)
    {
        var created = await Setup(storeId).CreateAsync(Draft(tcgId, capacity));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        return created.TournamentId!.Value;
    }

    /// <summary>Signs players up straight through the aggregate; the screen for this is tramo 3c.</summary>
    private async Task RegisterPlayers(Guid storeId, Guid tournamentId, int players)
    {
        var repository = new TournamentRepository(fixture.CreateContextFactory(storeId));

        var result = await repository.ExecuteAsync(tournamentId, tournament =>
        {
            for (var seat = 1; seat <= players; seat++)
            {
                tournament.Register($"Jugador {seat}");
            }

            return OperationResult.Ok();
        });

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    private async Task<Guid> NewApprovedStore()
    {
        await using var db = fixture.CreateContext();

        var store = new Store($"Tienda {Guid.CreateVersion7():N}", new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));
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
