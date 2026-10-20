using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TopCut.Domain.Catalog;
using TopCut.Domain.Events;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public class TournamentPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task The_whole_aggregate_survives_a_round_trip()
    {
        var tenantId = await NewApprovedStore();
        Guid tournamentId;

        await using (var db = fixture.CreateContext(tenantId))
        {
            var tournament = await NewTournament(db, tenantId);
            tournament.SetTopCut(4);
            tournament.Publish();
            var ana = tournament.SignUp("Ana", userId: $"user-{tenantId:N}");
            var bruno = tournament.SignUp("Bruno");
            var round = tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);
            var pairing = round.Pair(table: 1, ana.Id, bruno.Id);

            db.Tournaments.Add(tournament);
            await db.SaveChangesAsync();

            tournament.ReportResult(
                round.Id,
                pairing.Id,
                [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.PlayerA],
                ana.Id);
            await db.SaveChangesAsync();

            tournamentId = tournament.Id;
        }

        await using var reader = fixture.CreateContext(tenantId);
        var reloaded = await reader.Tournaments
            .Include(t => t.Participants)
            .Include(t => t.Rounds).ThenInclude(r => r.Pairings).ThenInclude(p => p.Games)
            .SingleAsync(t => t.Id == tournamentId);

        Assert.Equal(4, reloaded.TopCutSize);
        Assert.Equal(2, reloaded.Participants.Count);
        Assert.Contains(reloaded.Participants, p => p.UserId is not null);
        Assert.Contains(reloaded.Participants, p => p.UserId is null);

        var loadedRound = Assert.Single(reloaded.Rounds);
        Assert.Equal(MatchFormat.Bo3, loadedRound.Format);

        var loadedPairing = Assert.Single(loadedRound.Pairings);
        Assert.Equal(3, loadedPairing.Games.Count);
        Assert.Equal([1, 2, 3], loadedPairing.Games.OrderBy(g => g.Number).Select(g => g.Number));
        Assert.Equal(MatchResult.Win, loadedPairing.ResultFor(loadedPairing.PlayerAId));
    }

    [Fact]
    public async Task The_round_clock_survives_a_round_trip_as_absolute_instants()
    {
        var tenantId = await NewApprovedStore();
        var startedAt = new DateTimeOffset(2026, 9, 20, 17, 0, 0, TimeSpan.Zero);
        Guid tournamentId;

        await using (var db = fixture.CreateContext(tenantId))
        {
            var tournament = await NewTournament(db, tenantId);
            tournament.Publish();
            tournament.StartRound(Phase.Swiss, startedAt);
            db.Tournaments.Add(tournament);
            await db.SaveChangesAsync();
            tournamentId = tournament.Id;
        }

        await using var reader = fixture.CreateContext(tenantId);
        var round = Assert.Single((await reader.Tournaments
            .Include(t => t.Rounds)
            .SingleAsync(t => t.Id == tournamentId)).Rounds);

        // Reconnecting must not restart the countdown, which is why the end instant is stored.
        Assert.Equal(startedAt.AddMinutes(50), round.RegularTimeEndsAt);
        Assert.Equal(TimeSpan.FromMinutes(20), round.TimeRemaining(startedAt.AddMinutes(30)));
    }

    [Fact]
    public async Task The_settings_copy_is_stored_with_the_tournament_and_not_looked_up_live()
    {
        var tenantId = await NewApprovedStore();
        Guid tournamentId;
        int rulesetId;

        await using (var db = fixture.CreateContext(tenantId))
        {
            var tournament = await NewTournament(db, tenantId);
            db.Tournaments.Add(tournament);
            await db.SaveChangesAsync();

            tournamentId = tournament.Id;
            rulesetId = tournament.Settings.SourceRulesetId;
        }

        // The Superadmin later edits Magic's default ruleset.
        await using (var admin = fixture.CreateContext())
        {
            var ruleset = await admin.Rulesets.SingleAsync(r => r.Id == rulesetId);
            ruleset.SetPhase(Phase.Swiss, new PhaseSettings(MatchFormat.Bo1, TimeSpan.FromMinutes(20), TimeSpan.Zero));
            await admin.SaveChangesAsync();
        }

        try
        {
            await using var reader = fixture.CreateContext(tenantId);
            var reloaded = await reader.Tournaments.SingleAsync(t => t.Id == tournamentId);

            Assert.Equal(MatchFormat.Bo3, reloaded.Settings.SwissPhase.Format);
            Assert.Equal(TimeSpan.FromMinutes(50), reloaded.Settings.SwissPhase.RoundLength);
            Assert.Equal(rulesetId, reloaded.Settings.SourceRulesetId);
        }
        finally
        {
            // The catalog is shared by the whole collection, so put it back as it was.
            await using var restore = fixture.CreateContext();
            var shared = await restore.Rulesets.SingleAsync(r => r.Id == rulesetId);
            shared.SetPhase(Phase.Swiss, new PhaseSettings(MatchFormat.Bo3, TimeSpan.FromMinutes(50), TimeSpan.Zero));
            await restore.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Creation_and_update_timestamps_are_stamped_automatically()
    {
        var tenantId = await NewApprovedStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero));

        await using var db = fixture.CreateContext(tenantId, timeProvider: clock);
        var tournament = await NewTournament(db, tenantId);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        Assert.Equal(clock.GetUtcNow(), tournament.CreatedAt);
        Assert.Null(tournament.UpdatedAt);

        clock.Advance(TimeSpan.FromHours(2));
        tournament.Publish();
        await db.SaveChangesAsync();

        Assert.Equal(clock.GetUtcNow(), tournament.UpdatedAt);
    }

    [Fact]
    public async Task Two_simultaneous_edits_to_the_same_tournament_collide_instead_of_overwriting()
    {
        var tenantId = await NewApprovedStore();
        Guid tournamentId;

        await using (var db = fixture.CreateContext(tenantId))
        {
            var tournament = await NewTournament(db, tenantId);
            db.Tournaments.Add(tournament);
            await db.SaveChangesAsync();
            tournamentId = tournament.Id;
        }

        await using var first = fixture.CreateContext(tenantId);
        await using var second = fixture.CreateContext(tenantId);

        var fromFirst = await first.Tournaments.SingleAsync(t => t.Id == tournamentId);
        var fromSecond = await second.Tournaments.SingleAsync(t => t.Id == tournamentId);

        fromFirst.SetTopCut(8);
        await first.SaveChangesAsync();

        fromSecond.SetTopCut(4);

        // Without RowVersion this second save would silently win and erase the first change.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Domain_events_are_dispatched_once_the_save_succeeds()
    {
        var tenantId = await NewApprovedStore();
        var dispatcher = new RecordingDispatcher();

        await using var db = fixture.CreateContext(tenantId, dispatcher);
        var tournament = await NewTournament(db, tenantId);
        tournament.Publish();
        tournament.SignUp("Ana");
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        dispatcher.Dispatched.Clear();
        tournament.StartRound(Phase.Swiss, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        var started = Assert.Single(dispatcher.Dispatched.OfType<RoundStarted>());
        Assert.Equal(tournament.Id, started.TournamentId);

        // Drained on dispatch, so a later save cannot publish the same event twice.
        Assert.Empty(tournament.DomainEvents);
        dispatcher.Dispatched.Clear();
        await db.SaveChangesAsync();
        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task Two_walk_ins_without_an_account_can_share_the_same_tournament()
    {
        var tenantId = await NewApprovedStore();

        await using var db = fixture.CreateContext(tenantId);
        var tournament = await NewTournament(db, tenantId);
        tournament.SignUp("Jugador de mostrador 1");
        tournament.SignUp("Jugador de mostrador 2");
        db.Tournaments.Add(tournament);

        // The unique index on (TournamentId, UserId) is filtered, so NULL user ids do not collide.
        await db.SaveChangesAsync();

        Assert.Equal(2, tournament.Participants.Count);
    }

    /// <summary>Creates an approved store and returns its id, which is also the tenant id.</summary>
    private async Task<Guid> NewApprovedStore()
    {
        await using var setup = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(setup);

        var store = new Store("Tienda de pruebas", new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));
        store.Approve();

        setup.Stores.Add(store);
        await setup.SaveChangesAsync();

        return store.Id;
    }

    private static async Task<Tournament> NewTournament(TopCutDbContext db, Guid tenantId)
    {
        var ruleset = await db.Rulesets
            .Include(r => r.Tcg)
            .FirstAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);

        return Tournament.Create(
            tenantId,
            ruleset,
            "Torneo de prueba",
            DateTimeOffset.UtcNow.AddDays(7),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity: 8,
            swissRounds: 3);
    }
}
