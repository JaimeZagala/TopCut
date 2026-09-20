using Microsoft.EntityFrameworkCore;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The engine driven the way the application will drive it: the aggregate is reloaded from SQL
/// Server between every single operation, exactly as it would be across separate web requests.
/// <para>
/// The domain tests already prove the pairing and scoring maths. What these add is that nothing
/// needed to compute them is lost on the way to the database and back — a bug that unit tests
/// holding one object in memory can never catch.
/// </para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public class TournamentEngineTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_full_swiss_tournament_runs_with_the_aggregate_reloaded_between_rounds()
    {
        var (tenantId, tournamentId) = await NewTournament(players: 8, swissRounds: 3);

        for (var round = 0; round < 3; round++)
        {
            Assert.True((await Service(tenantId).StartNextRoundAsync(tournamentId)).Succeeded);
            await ReportEverything(tenantId, tournamentId);
            await CloseCurrentRound(tenantId, tournamentId);
        }

        Assert.True((await Service(tenantId).CompleteAsync(tournamentId)).Succeeded);

        var standings = await Service(tenantId).GetStandingsAsync(tournamentId);

        Assert.Equal(8, standings.Count);
        Assert.All(standings, row => Assert.Equal(3, row.RoundsPlayed));
        Assert.Equal(Enumerable.Range(1, 8), standings.Select(row => row.Rank).Order());

        // The leader won all three. If any result had been lost in a round trip this would not hold.
        Assert.Equal(9, standings.Single(row => row.Rank == 1).MatchPoints);
    }

    [Fact]
    public async Task Nobody_is_paired_against_the_same_opponent_twice_across_reloads()
    {
        var (tenantId, tournamentId) = await NewTournament(players: 8, swissRounds: 3);

        for (var round = 0; round < 3; round++)
        {
            await Service(tenantId).StartNextRoundAsync(tournamentId);
            await ReportEverything(tenantId, tournamentId);
            await CloseCurrentRound(tenantId, tournamentId);
        }

        await using var db = fixture.CreateContext(tenantId);
        var tournament = await Load(db, tournamentId);

        var matchups = tournament.Rounds
            .SelectMany(round => round.Pairings)
            .Where(pairing => !pairing.IsBye)
            .Select(pairing => pairing.PlayerAId.CompareTo(pairing.PlayerBId!.Value) < 0
                ? (pairing.PlayerAId, pairing.PlayerBId!.Value)
                : (pairing.PlayerBId!.Value, pairing.PlayerAId))
            .ToList();

        // Rematch avoidance depends on reading back every round already played, so this is really
        // a test that the round history survives persistence.
        Assert.Equal(12, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public async Task The_top_cut_is_seeded_from_the_standings_read_back_from_the_database()
    {
        var (tenantId, tournamentId) = await NewTournament(players: 8, swissRounds: 3, topCut: 4);

        for (var round = 0; round < 3; round++)
        {
            await Service(tenantId).StartNextRoundAsync(tournamentId);
            await ReportEverything(tenantId, tournamentId);
            await CloseCurrentRound(tenantId, tournamentId);
        }

        var finalSwiss = await Service(tenantId).GetStandingsAsync(tournamentId);
        var qualifiers = finalSwiss.OrderBy(row => row.Rank).Take(4).Select(row => row.ParticipantId).ToList();

        Assert.True((await Service(tenantId).StartNextRoundAsync(tournamentId)).Succeeded);

        await using var db = fixture.CreateContext(tenantId);
        var tournament = await Load(db, tournamentId);
        var semis = tournament.Rounds.Single(round => round.Phase == Phase.TopCut);
        var tables = semis.Pairings.OrderBy(pairing => pairing.Table).ToList();

        Assert.Equal(2, tables.Count);
        Assert.Equal((qualifiers[0], qualifiers[3]), (tables[0].PlayerAId, tables[0].PlayerBId));
        Assert.Equal((qualifiers[1], qualifiers[2]), (tables[1].PlayerAId, tables[1].PlayerBId));
    }

    [Fact]
    public async Task A_tournament_with_no_rounds_left_says_so_instead_of_starting_one()
    {
        var (tenantId, tournamentId) = await NewTournament(players: 4, swissRounds: 1);

        await Service(tenantId).StartNextRoundAsync(tournamentId);
        await ReportEverything(tenantId, tournamentId);
        await CloseCurrentRound(tenantId, tournamentId);

        Assert.False(await Service(tenantId).HasNextRoundAsync(tournamentId));

        var result = await Service(tenantId).StartNextRoundAsync(tournamentId);

        Assert.False(result.Succeeded);
        Assert.Contains("todas sus rondas", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task A_broken_invariant_comes_back_as_a_message_and_not_as_a_crash()
    {
        var (tenantId, tournamentId) = await NewTournament(players: 4, swissRounds: 2);
        await Service(tenantId).StartNextRoundAsync(tournamentId);

        await using var db = fixture.CreateContext(tenantId);
        var tournament = await Load(db, tournamentId);
        var roundId = tournament.Rounds.Single().Id;

        var result = await Service(tenantId).CloseRoundAsync(tournamentId, roundId);

        Assert.False(result.Succeeded);
        Assert.Contains("resultados pendientes", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Another_store_cannot_advance_a_tournament_that_is_not_theirs()
    {
        var (_, tournamentId) = await NewTournament(players: 4, swissRounds: 2);
        var intruder = await NewApprovedStore();

        var result = await Service(intruder).StartNextRoundAsync(tournamentId);

        // The tenant filter makes "not yours" and "does not exist" the same answer, which is the
        // right amount to tell someone poking at ids.
        Assert.False(result.Succeeded);
        Assert.Contains("No existe ese torneo", string.Join(" ", result.Errors));
        Assert.Empty(await Service(intruder).GetStandingsAsync(tournamentId));
    }

    // --- Harness -----------------------------------------------------------------------------------

    private TournamentService Service(Guid tenantId) =>
        new(new TournamentRepository(fixture.CreateContextFactory(tenantId)), TimeProvider.System);

    private static Task<Tournament> Load(TopCutDbContext db, Guid tournamentId) =>
        db.Tournaments
            .Include(t => t.Participants)
            .Include(t => t.Rounds).ThenInclude(r => r.Pairings).ThenInclude(p => p.Games)
            .AsSplitQuery()
            .SingleAsync(t => t.Id == tournamentId);

    /// <summary>Reports every open match of the round in progress, reloading for each one.</summary>
    private async Task ReportEverything(Guid tenantId, Guid tournamentId)
    {
        List<(Guid RoundId, Guid PairingId)> pending;

        await using (var db = fixture.CreateContext(tenantId))
        {
            var tournament = await Load(db, tournamentId);
            var round = tournament.CurrentRound!;

            pending = [.. round.Pairings
                .Where(pairing => pairing.Status == PairingStatus.AwaitingResult)
                .Select(pairing => (round.Id, pairing.Id))];
        }

        foreach (var (roundId, pairingId) in pending)
        {
            // Player A always wins, so the tournament is deterministic and the leader is knowable.
            var reported = await Service(tenantId).ReportResultAsync(
                tournamentId,
                roundId,
                pairingId,
                [GameWinner.PlayerA, GameWinner.PlayerA],
                reportedBy: null);

            Assert.True(reported.Succeeded, string.Join(" ", reported.Errors));
        }
    }

    private async Task CloseCurrentRound(Guid tenantId, Guid tournamentId)
    {
        Guid roundId;

        await using (var db = fixture.CreateContext(tenantId))
        {
            roundId = (await Load(db, tournamentId)).CurrentRound!.Id;
        }

        var closed = await Service(tenantId).CloseRoundAsync(tournamentId, roundId);
        Assert.True(closed.Succeeded, string.Join(" ", closed.Errors));
    }

    private async Task<(Guid TenantId, Guid TournamentId)> NewTournament(int players, int swissRounds, int? topCut = null)
    {
        var tenantId = await NewApprovedStore();

        await using var db = fixture.CreateContext(tenantId);
        var ruleset = await db.Rulesets.Include(r => r.Tcg).FirstAsync(r => r.Tcg.Code == SeedCatalog.MagicCode);

        var tournament = Tournament.Create(
            tenantId,
            ruleset,
            "Torneo de motor",
            DateTimeOffset.UtcNow.AddDays(1),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity: players,
            swissRounds: swissRounds);

        if (topCut is { } size)
        {
            tournament.SetTopCut(size);
        }

        tournament.Publish();

        for (var seed = 1; seed <= players; seed++)
        {
            tournament.Register($"P{seed}");
        }

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        return (tenantId, tournament.Id);
    }

    private async Task<Guid> NewApprovedStore()
    {
        await using var setup = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(setup);

        var store = new Store("Tienda del motor", new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"));
        store.Approve();

        setup.Stores.Add(store);
        await setup.SaveChangesAsync();

        return store.Id;
    }
}
