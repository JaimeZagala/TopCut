using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Application.Tournaments;
using TopCut.Domain.Catalog;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Persistence;
using TopCut.Web.Components.Pages.Store;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// The organiser's control desk: launching rounds, taking results, correcting them and closing the
/// tournament. The engine itself is already covered elsewhere; what these pin down is the view the
/// screen is built from and the contract between the buttons it shows and what the domain accepts.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DeskTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Before_anything_is_played_the_desk_offers_the_first_round_and_nothing_else()
    {
        var (store, tournament) = await ReadyTournament(players: 4);

        var desk = await Desk(store, tournament);

        Assert.NotNull(desk);
        Assert.True(desk!.CanStartNextRound);
        Assert.Equal(Phase.Swiss, desk.NextPhase);
        Assert.Equal(1, desk.NextRoundNumber);
        Assert.Empty(desk.Rounds);
        Assert.Null(desk.Round);
        Assert.False(desk.CanCloseRound);
        Assert.False(desk.CanComplete);
    }

    [Fact]
    public async Task Launching_a_round_lays_out_the_tables_with_the_players_names_on_them()
    {
        var (store, tournament) = await ReadyTournament(players: 4);

        Assert.True((await Service(store).StartNextRoundAsync(tournament)).Succeeded);

        var desk = (await Desk(store, tournament))!;
        var round = desk.Round!;

        Assert.Equal(1, round.Number);
        Assert.Equal(RoundStatus.InProgress, round.Status);
        Assert.Equal(2, round.Matches.Count);
        Assert.Equal([1, 2], round.Matches.Select(match => match.Table));

        // Names, not ids: the desk is read out loud to a room.
        Assert.All(round.Matches, match => Assert.False(string.IsNullOrWhiteSpace(match.PlayerAName)));
        Assert.All(round.Matches, match => Assert.False(string.IsNullOrWhiteSpace(match.PlayerBName)));

        Assert.Equal(2, round.Pending);
        Assert.False(desk.CanCloseRound);
        Assert.False(desk.CanStartNextRound);
    }

    [Fact]
    public async Task An_odd_field_gets_a_bye_that_needs_no_result()
    {
        var (store, tournament) = await ReadyTournament(players: 5);

        await Service(store).StartNextRoundAsync(tournament);

        var round = (await Desk(store, tournament))!.Round!;
        var bye = Assert.Single(round.Matches, match => match.IsBye);

        Assert.Null(bye.PlayerBName);
        Assert.False(bye.Pending);

        // Two real matches to report, plus the bye that is already settled.
        Assert.Equal(2, round.Pending);
    }

    // --- The buttons the desk shows ---------------------------------------------------------------

    [Theory]
    [InlineData(MatchFormat.Bo1)]
    [InlineData(MatchFormat.Bo3)]
    public async Task Every_result_the_desk_offers_is_one_the_rules_accept(MatchFormat format)
    {
        // Enough tables to try every button in the same round.
        var outcomes = MatchOutcome.For(format);
        var (store, tournament) = await ReadyTournament(players: outcomes.Count * 2, format: format);

        await Service(store).StartNextRoundAsync(tournament);

        var round = (await Desk(store, tournament))!.Round!;

        foreach (var (match, outcome) in round.Matches.Zip(outcomes))
        {
            var reported = await Service(store).ReportResultAsync(
                tournament,
                round.RoundId,
                match.PairingId,
                outcome.Games,
                reportedBy: null);

            Assert.True(reported.Succeeded, $"'{outcome.Label}' fue rechazado: {string.Join(" ", reported.Errors)}");
        }

        // And each one is stored as the score its label promised: a button marked 2-1 must not
        // quietly become a 2-0 on the standings.
        var after = (await Desk(store, tournament))!.Round!;

        foreach (var (match, outcome) in after.Matches.Zip(outcomes))
        {
            Assert.Equal(outcome.Games.Count(game => game == GameWinner.PlayerA), match.PlayerAWins);
            Assert.Equal(outcome.Games.Count(game => game == GameWinner.PlayerB), match.PlayerBWins);
            Assert.Equal(outcome.IsDraw, match.PlayerAWins == match.PlayerBWins);
        }
    }

    // --- Running a round through ---------------------------------------------------------------------

    [Fact]
    public async Task A_round_can_only_be_closed_once_every_table_has_reported()
    {
        var (store, tournament) = await ReadyTournament(players: 4);
        await Service(store).StartNextRoundAsync(tournament);

        var round = (await Desk(store, tournament))!.Round!;

        await ReportFirstWin(store, tournament, round.RoundId, round.Matches[0]);

        var half = (await Desk(store, tournament))!;
        Assert.False(half.CanCloseRound);
        Assert.Equal(1, half.Round!.Pending);

        await ReportFirstWin(store, tournament, round.RoundId, round.Matches[1]);

        var whole = (await Desk(store, tournament))!;
        Assert.True(whole.CanCloseRound);
        Assert.True(whole.Round!.AllReported);
    }

    [Fact]
    public async Task Closing_the_last_round_leaves_only_the_option_to_finish()
    {
        var (store, tournament) = await ReadyTournament(players: 4, swissRounds: 1);

        await PlayRound(store, tournament);

        var desk = (await Desk(store, tournament))!;

        Assert.False(desk.CanStartNextRound);
        Assert.Null(desk.NextPhase);
        Assert.True(desk.CanComplete);

        Assert.True((await Service(store).CompleteAsync(tournament)).Succeeded);
        Assert.Equal(TournamentStatus.Completed, (await Desk(store, tournament))!.Status);
    }

    [Fact]
    public async Task After_the_swiss_rounds_the_desk_offers_the_top_cut()
    {
        var (store, tournament) = await ReadyTournament(players: 4, swissRounds: 1, topCut: 4);

        await PlayRound(store, tournament);

        var desk = (await Desk(store, tournament))!;

        Assert.True(desk.CanStartNextRound);
        Assert.Equal(Phase.TopCut, desk.NextPhase);

        await Service(store).StartNextRoundAsync(tournament);

        var cut = (await Desk(store, tournament))!.Round!;

        Assert.Equal(Phase.TopCut, cut.Phase);
        Assert.Equal(2, cut.Matches.Count);
    }

    [Fact]
    public async Task The_standings_follow_the_results_as_they_come_in()
    {
        var (store, tournament) = await ReadyTournament(players: 4);
        await Service(store).StartNextRoundAsync(tournament);

        var round = (await Desk(store, tournament))!.Round!;

        foreach (var match in round.Matches)
        {
            await ReportFirstWin(store, tournament, round.RoundId, match);
        }

        var standings = (await Desk(store, tournament))!.Standings;

        Assert.Equal(4, standings.Count);
        Assert.Equal([1, 2, 3, 4], standings.Select(row => row.Rank));

        // Two winners on three points, two losers on none.
        Assert.Equal(2, standings.Count(row => row.MatchPoints == 3));
        Assert.Equal(2, standings.Count(row => row.MatchPoints == 0));
    }

    [Fact]
    public async Task A_corrected_result_is_marked_as_such_and_moves_the_standings()
    {
        var (store, tournament) = await ReadyTournament(players: 2);
        await Service(store).StartNextRoundAsync(tournament);

        var round = (await Desk(store, tournament))!.Round!;
        var match = round.Matches.Single();

        await ReportFirstWin(store, tournament, round.RoundId, match);
        var winner = (await Desk(store, tournament))!.Standings.First(row => row.MatchPoints == 3).Name;

        // The players came back and said it was the other way round.
        var amended = await Service(store).AmendResultAsync(
            tournament,
            round.RoundId,
            match.PairingId,
            [GameWinner.PlayerB, GameWinner.PlayerB]);

        Assert.True(amended.Succeeded, string.Join(" ", amended.Errors));

        var desk = (await Desk(store, tournament))!;

        Assert.True(desk.Round!.Matches.Single().AmendedByOrganizer);
        Assert.NotEqual(winner, desk.Standings.First(row => row.MatchPoints == 3).Name);
    }

    [Fact]
    public async Task A_round_whose_clock_ran_out_can_be_settled_in_one_go()
    {
        var (store, tournament) = await ReadyTournament(players: 4);
        await Service(store).StartNextRoundAsync(tournament);

        // Nothing reported and the clock long gone.
        var later = Service(store, DateTimeOffset.UtcNow.AddHours(3));
        var desk = await later.GetDeskAsync(tournament);

        Assert.True(desk!.CanApplyTimeExpiry);

        var applied = await later.ApplyTimeExpiryAsync(tournament, desk.Round!.RoundId);

        Assert.True(applied.Succeeded, string.Join(" ", applied.Errors));

        var after = (await later.GetDeskAsync(tournament))!;

        Assert.True(after.Round!.AllReported);
        Assert.All(after.Round.Matches.Where(match => !match.IsBye), match => Assert.True(match.DecidedByTimeExpiry));
        Assert.True(after.CanCloseRound);
    }

    // --- Looking back ----------------------------------------------------------------------------------

    [Fact]
    public async Task An_earlier_round_can_be_brought_back_up_to_be_corrected()
    {
        var (store, tournament) = await ReadyTournament(players: 4, swissRounds: 2);

        await PlayRound(store, tournament);
        await Service(store).StartNextRoundAsync(tournament);

        // With round two open, the desk defaults to it but round one is still reachable.
        Assert.Equal(2, (await Desk(store, tournament))!.Round!.Number);

        var first = await Service(store).GetDeskAsync(tournament, roundNumber: 1);

        Assert.Equal(1, first!.Round!.Number);
        Assert.Equal(RoundStatus.Closed, first.Round.Status);
        Assert.Equal(2, first.Rounds.Count);
    }

    [Fact]
    public async Task The_rounds_come_back_in_the_order_they_were_played()
    {
        // Four rounds, so the database has room to hand them back in whatever order it likes. It
        // did exactly that: the desk showed the buttons as 2, 4, 3, 1.
        var (store, tournament) = await ReadyTournament(players: 4, swissRounds: 4);

        for (var round = 0; round < 4; round++)
        {
            await PlayRound(store, tournament);
        }

        var desk = (await Desk(store, tournament))!;

        Assert.Equal([1, 2, 3, 4], desk.Rounds.Select(round => round.Number));

        // And with no round asked for, the desk opens on the last one played, not on whichever
        // row came back first.
        Assert.Equal(4, desk.Round!.Number);
    }

    [Fact]
    public async Task A_bracket_of_several_rounds_always_pairs_from_the_one_just_played()
    {
        // Eight players into a top 8: quarters, semis, final. Each bracket round is built from the
        // previous one, so picking the wrong "previous" would pair the wrong people — quietly.
        var (store, tournament) = await ReadyTournament(players: 8, swissRounds: 1, topCut: 8);

        await PlayRound(store, tournament);

        await PlayRound(store, tournament);
        Assert.Equal(4, (await Desk(store, tournament))!.Round!.Matches.Count);

        await PlayRound(store, tournament);
        Assert.Equal(2, (await Desk(store, tournament))!.Round!.Matches.Count);

        await PlayRound(store, tournament);

        var desk = (await Desk(store, tournament))!;

        Assert.Single(desk.Round!.Matches);
        Assert.Equal([1, 2, 3, 4], desk.Rounds.Select(round => round.Number));
        Assert.False(desk.CanStartNextRound);
    }

    [Fact]
    public async Task Another_stores_desk_is_not_reachable()
    {
        var (_, tournament) = await ReadyTournament(players: 4);
        var intruder = await NewApprovedStore();

        Assert.Null(await Desk(intruder, tournament));
    }

    // --- Harness -------------------------------------------------------------------------------------------

    private TournamentService Service(Guid storeId, DateTimeOffset? now = null) => new(
        new TournamentRepository(fixture.CreateContextFactory(storeId)),
        now is { } instant ? new FixedClock(instant) : TimeProvider.System);

    private Task<TournamentDesk?> Desk(Guid storeId, Guid tournamentId) =>
        Service(storeId).GetDeskAsync(tournamentId);

    private Task<OperationResult> ReportFirstWin(Guid storeId, Guid tournamentId, Guid roundId, DeskMatch match) =>
        Service(storeId).ReportResultAsync(
            tournamentId,
            roundId,
            match.PairingId,
            [GameWinner.PlayerA, GameWinner.PlayerA],
            reportedBy: null);

    /// <summary>Opens a round, reports every table to player A, and closes it.</summary>
    private async Task PlayRound(Guid storeId, Guid tournamentId)
    {
        Assert.True((await Service(storeId).StartNextRoundAsync(tournamentId)).Succeeded);

        var round = (await Desk(storeId, tournamentId))!.Round!;

        foreach (var match in round.Matches.Where(match => match.Pending))
        {
            var reported = await ReportFirstWin(storeId, tournamentId, round.RoundId, match);
            Assert.True(reported.Succeeded, string.Join(" ", reported.Errors));
        }

        var closed = await Service(storeId).CloseRoundAsync(tournamentId, round.RoundId);
        Assert.True(closed.Succeeded, string.Join(" ", closed.Errors));
    }

    /// <summary>A published tournament with its players already signed up, ready to start.</summary>
    private async Task<(Guid StoreId, Guid TournamentId)> ReadyTournament(
        int players,
        int swissRounds = 3,
        int? topCut = null,
        MatchFormat format = MatchFormat.Bo3)
    {
        var storeId = await NewApprovedStore();

        await using var db = fixture.CreateContext();
        await CatalogSeeder.SeedAsync(db);
        var magic = (await db.Tcgs.SingleAsync(tcg => tcg.Code == SeedCatalog.MagicCode)).Id;

        var setup = new TournamentSetupService(
            new TournamentRepository(fixture.CreateContextFactory(storeId)),
            new TcgCatalog(fixture.CreateContextFactory(storeId)),
            fixture.CreateLeagues(storeId),
            new StoreScope(storeId));

        var created = await setup.CreateAsync(
            new TournamentDraft(
                magic,
                "Torneo de mesa",
                DateTimeOffset.UtcNow.AddHours(1),
                "Calle Mayor 1",
                "Valencia",
                "Valencia",
                "46001",
                Capacity: players,
                swissRounds,
                topCut),
            new TournamentRules(
                new PhaseSettings(format, TimeSpan.FromMinutes(50), TimeSpan.Zero),
                new PhaseSettings(format, TimeSpan.FromMinutes(90), TimeSpan.Zero),
                TimeExpiryPolicy.LeaderWins));

        Assert.True(created.Succeeded, string.Join(" ", created.Errors));
        var tournamentId = created.TournamentId!.Value;

        var roster = new ParticipantService(
            new TournamentRepository(fixture.CreateContextFactory(storeId)),
            fixture.CreateStorePlayers(storeId));

        for (var seat = 1; seat <= players; seat++)
        {
            var signedUp = await roster.RegisterAsync(tournamentId, $"Jugador {seat}");
            Assert.True(signedUp.Succeeded, string.Join(" ", signedUp.Errors));
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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
