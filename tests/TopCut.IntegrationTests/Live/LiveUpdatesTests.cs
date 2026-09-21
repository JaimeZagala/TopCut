using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Events;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Live;
using TopCut.Infrastructure.Persistence;
using TopCut.IntegrationTests.Persistence;

namespace TopCut.IntegrationTests.Live;

/// <summary>
/// The whole chain, for the first time: an organiser saves something, the EF interceptor picks up
/// the domain events <b>after</b> the commit, the dispatcher finds the handlers, and the screens
/// watching that tournament are told.
/// <para>
/// Until milestone 4 the domain events were raised and dropped on purpose (ADR-0010). This is the
/// test that proves the seam was real and not just a nice idea in a document.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class LiveUpdatesTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Starting_a_round_reaches_the_screens_watching_that_tournament()
    {
        var live = Broadcaster();
        var (store, tournament) = await ReadyTournament(players: 4);
        var seen = Listen(live, tournament);

        var started = await Service(store, live).StartNextRoundAsync(tournament);

        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
        Assert.Contains(TournamentChange.RoundStarted, seen);
    }

    [Fact]
    public async Task A_reported_result_reaches_the_screens()
    {
        var live = Broadcaster();
        var (store, tournament) = await ReadyTournament(players: 4);
        var service = Service(store, live);

        await service.StartNextRoundAsync(tournament);

        var round = (await service.GetDeskAsync(tournament))!.Round!;
        var seen = Listen(live, tournament);

        await service.ReportResultAsync(
            tournament,
            round.RoundId,
            round.Matches[0].PairingId,
            [GameWinner.PlayerA, GameWinner.PlayerA],
            reportedBy: null);

        Assert.Contains(TournamentChange.ResultRecorded, seen);
    }

    [Fact]
    public async Task Closing_a_round_reaches_the_screens_so_the_room_stops_seeing_a_finished_round()
    {
        var live = Broadcaster();
        var (store, tournament) = await ReadyTournament(players: 4);
        var service = Service(store, live);

        await service.StartNextRoundAsync(tournament);
        var round = (await service.GetDeskAsync(tournament))!.Round!;

        foreach (var match in round.Matches)
        {
            await service.ReportResultAsync(
                tournament,
                round.RoundId,
                match.PairingId,
                [GameWinner.PlayerA, GameWinner.PlayerA],
                reportedBy: null);
        }

        var seen = Listen(live, tournament);

        var closed = await service.CloseRoundAsync(tournament, round.RoundId);

        Assert.True(closed.Succeeded, string.Join(" ", closed.Errors));
        Assert.Contains(TournamentChange.RoundClosed, seen);
    }

    [Fact]
    public async Task A_refused_change_tells_nobody()
    {
        var live = Broadcaster();
        var (store, tournament) = await ReadyTournament(players: 4);
        var seen = Listen(live, tournament);

        // Two rounds at once is not allowed, so nothing was committed and nothing should be
        // announced: a screen must never be shown work that was rolled back.
        await Service(store, live).StartNextRoundAsync(tournament);
        seen.Clear();

        var refused = await Service(store, live).StartNextRoundAsync(tournament);

        Assert.False(refused.Succeeded);
        Assert.Empty(seen);
    }

    [Fact]
    public async Task Dropping_somebody_reaches_the_screens_as_a_roster_change()
    {
        var live = Broadcaster();
        var (store, tournament) = await ReadyTournament(players: 4);

        var roster = new ParticipantService(Repository(store, live));
        var queries = new TournamentQueries(fixture.CreateContextFactory(store), TimeProvider.System);
        var player = (await queries.RosterAsync(tournament)).Registered.First();

        var seen = Listen(live, tournament);

        var dropped = await roster.DropAsync(tournament, player.Id);

        Assert.True(dropped.Succeeded, string.Join(" ", dropped.Errors));
        Assert.Contains(TournamentChange.RosterChanged, seen);
    }

    [Fact]
    public async Task Screens_on_other_tournaments_are_left_alone()
    {
        var live = Broadcaster();
        var (store, mine) = await ReadyTournament(players: 4);
        var (_, theirs) = await ReadyTournament(players: 4);

        var elsewhere = Listen(live, theirs);

        await Service(store, live).StartNextRoundAsync(mine);

        Assert.Empty(elsewhere);
    }

    // --- Harness -------------------------------------------------------------------------------

    /// <summary>The real broadcaster with the real handlers behind the real dispatcher.</summary>
    private static TournamentBroadcaster Broadcaster() => new(NullLogger<TournamentBroadcaster>.Instance);

    private static List<TournamentChange> Listen(TournamentBroadcaster live, Guid tournamentId)
    {
        var seen = new List<TournamentChange>();

        live.Watch(tournamentId, change =>
        {
            seen.Add(change);
            return Task.CompletedTask;
        });

        return seen;
    }

    private TournamentRepository Repository(Guid storeId, TournamentBroadcaster live)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITournamentBroadcaster>(live);
        services.AddScoped<IDomainEventHandler<RoundStarted>, LiveUpdates.OnRoundStarted>();
        services.AddScoped<IDomainEventHandler<RoundClosed>, LiveUpdates.OnRoundClosed>();
        services.AddScoped<IDomainEventHandler<ResultRecorded>, LiveUpdates.OnResultRecorded>();
        services.AddScoped<IDomainEventHandler<RoundTimeExpired>, LiveUpdates.OnRoundTimeExpired>();
        services.AddScoped<IDomainEventHandler<ParticipantRegistered>, LiveUpdates.OnParticipantRegistered>();
        services.AddScoped<IDomainEventHandler<ParticipantDropped>, LiveUpdates.OnParticipantDropped>();
        services.AddScoped<IDomainEventHandler<TournamentCompleted>, LiveUpdates.OnTournamentCompleted>();

        var dispatcher = new DomainEventDispatcher(services.BuildServiceProvider());

        return new TournamentRepository(fixture.CreateContextFactory(storeId, dispatcher));
    }

    private TournamentService Service(Guid storeId, TournamentBroadcaster live) =>
        new(Repository(storeId, live), TimeProvider.System);

    private async Task<(Guid StoreId, Guid TournamentId)> ReadyTournament(int players)
    {
        var storeId = await NewApprovedStore();

        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        var magic = (await db.Tcgs.SingleAsync(tcg => tcg.Code == SeedCatalog.MagicCode)).Id;

        var setup = new TournamentSetupService(
            new TournamentRepository(fixture.CreateContextFactory(storeId)),
            new TcgCatalog(fixture.CreateContextFactory(storeId)),
            new StoreScope(storeId));

        var created = await setup.CreateAsync(new TournamentDraft(
            magic,
            "Torneo en vivo",
            DateTimeOffset.UtcNow.AddHours(1),
            "Calle Mayor 1",
            "Valencia",
            "Valencia",
            "46001",
            Capacity: players,
            SwissRounds: 3,
            TopCutSize: null));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        var tournamentId = created.TournamentId!.Value;

        var roster = new ParticipantService(new TournamentRepository(fixture.CreateContextFactory(storeId)));

        for (var seat = 1; seat <= players; seat++)
        {
            await roster.RegisterAsync(tournamentId, $"Jugador {seat}");
        }

        Assert.True((await setup.PublishAsync(tournamentId)).Succeeded);

        return (storeId, tournamentId);
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
