using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Events;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Tournaments;

public class RoundAndPairingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 17, 0, 0, TimeSpan.Zero);

    private static Tournament TournamentFor(Ruleset ruleset) => Tournament.Create(
        Guid.CreateVersion7(),
        ruleset,
        "Torneo de prueba",
        Now.AddDays(1),
        new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
        capacity: 16,
        swissRounds: 4);

    private static Tournament Magic() => TournamentFor(SeedCatalog.MagicRuleset(tcgId: 1));

    private static Tournament OnePiece() => TournamentFor(SeedCatalog.OnePieceRuleset(tcgId: 2));

    // --- Clock -----------------------------------------------------------------------------------

    [Fact]
    public void A_round_stores_the_end_instant_rather_than_seconds_remaining()
    {
        var tournament = Magic();
        tournament.Publish();

        var round = tournament.StartRound(Phase.Swiss, Now);

        Assert.Equal(Now.AddMinutes(50), round.RegularTimeEndsAt);
        Assert.Equal(TimeSpan.FromMinutes(30), round.TimeRemaining(Now.AddMinutes(20)));
    }

    [Fact]
    public void OnePiece_adds_overtime_to_the_rounds_hard_deadline()
    {
        var tournament = OnePiece();
        tournament.Publish();

        var round = tournament.StartRound(Phase.Swiss, Now);

        Assert.Equal(Now.AddMinutes(30), round.RegularTimeEndsAt);
        Assert.Equal(Now.AddMinutes(35), round.ExtraTimeEndsAt);
    }

    [Fact]
    public void Time_remaining_never_goes_negative()
    {
        var tournament = Magic();
        tournament.Publish();
        var round = tournament.StartRound(Phase.Swiss, Now);

        Assert.Equal(TimeSpan.Zero, round.TimeRemaining(Now.AddHours(3)));
    }

    // --- Byes --------------------------------------------------------------------------------------

    [Fact]
    public void A_bye_is_born_already_resolved_as_an_automatic_win()
    {
        var tournament = Magic();
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        var round = tournament.StartRound(Phase.Swiss, Now);

        var bye = round.Pair(table: 1, ana.Id, playerBId: null);

        Assert.True(bye.IsBye);
        Assert.Equal(PairingStatus.Reported, bye.Status);
        Assert.Equal(MatchResult.Bye, bye.ResultFor(ana.Id));
    }

    [Fact]
    public void A_bye_takes_no_reported_result()
    {
        var tournament = Magic();
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        var round = tournament.StartRound(Phase.Swiss, Now);
        var bye = round.Pair(table: 1, ana.Id, playerBId: null);

        Assert.Throws<DomainException>(() => tournament.ReportResult(
            round.Id, bye.Id, [GameWinner.PlayerA], reportedBy: null));
    }

    [Fact]
    public void Nobody_is_paired_against_themselves()
    {
        var tournament = Magic();
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        var round = tournament.StartRound(Phase.Swiss, Now);

        Assert.Throws<DomainException>(() => round.Pair(table: 1, ana.Id, ana.Id));
    }

    // --- Reporting results ----------------------------------------------------------------------------

    [Fact]
    public void Bo3_stores_every_game_and_not_just_the_overall_result()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(Magic());

        tournament.ReportResult(
            round.Id,
            pairing.Id,
            [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.PlayerA],
            reportedBy: ana.Id);

        Assert.Equal(3, pairing.Games.Count);
        Assert.Equal([1, 2, 3], pairing.Games.Select(g => g.Number));
        Assert.Equal(2, pairing.GamesWonBy(ana.Id));
        Assert.Equal(1, pairing.GamesWonBy(bruno.Id));
        Assert.Equal(MatchResult.Win, pairing.ResultFor(ana.Id));
        Assert.Equal(MatchResult.Loss, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void Bo1_is_decided_by_a_single_game()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(OnePiece());

        tournament.ReportResult(round.Id, pairing.Id, [GameWinner.PlayerB], ana.Id);

        Assert.Equal(MatchResult.Win, pairing.ResultFor(bruno.Id));
        Assert.Equal(MatchResult.Loss, pairing.ResultFor(ana.Id));
    }

    [Fact]
    public void Bo1_has_no_room_for_a_second_game()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(OnePiece());

        var error = Assert.Throws<DomainException>(() => tournament.ReportResult(
            round.Id, pairing.Id, [GameWinner.PlayerA, GameWinner.PlayerB], ana.Id));

        Assert.Contains("no admite más de 1", error.Message);
    }

    [Fact]
    public void Bo3_rejects_games_played_after_the_match_was_already_decided()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(Magic());

        // Ana wins the first two: the third was never played.
        var error = Assert.Throws<DomainException>(() => tournament.ReportResult(
            round.Id,
            pairing.Id,
            [GameWinner.PlayerA, GameWinner.PlayerA, GameWinner.PlayerB],
            ana.Id));

        Assert.Contains("ya estaba decidido", error.Message);
    }

    [Fact]
    public void Bo3_accepts_a_two_nil_reported_as_two_games()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(Magic());

        tournament.ReportResult(round.Id, pairing.Id, [GameWinner.PlayerA, GameWinner.PlayerA], ana.Id);

        Assert.Equal(2, pairing.Games.Count);
        Assert.Equal(MatchResult.Win, pairing.ResultFor(ana.Id));
        Assert.Equal(0, pairing.GamesWonBy(bruno.Id));
    }

    [Fact]
    public void A_Bo3_that_ends_one_all_with_a_drawn_game_is_a_draw()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(Magic());

        tournament.ReportResult(
            round.Id,
            pairing.Id,
            [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.Draw],
            ana.Id);

        Assert.Equal(MatchResult.Draw, pairing.ResultFor(ana.Id));
        Assert.Equal(MatchResult.Draw, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void The_store_can_amend_a_result_and_it_is_flagged()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(OnePiece());
        tournament.ReportResult(round.Id, pairing.Id, [GameWinner.PlayerA], ana.Id);

        tournament.AmendResult(round.Id, pairing.Id, [GameWinner.PlayerB]);

        Assert.True(pairing.AmendedByOrganizer);
        Assert.Null(pairing.ReportedBy);
        Assert.Equal(MatchResult.Win, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void A_pairing_with_no_result_cannot_be_queried()
    {
        var (_, _, pairing, ana, _) = MatchInProgress(Magic());

        var error = Assert.Throws<DomainException>(() => pairing.ResultFor(ana.Id));

        Assert.Contains("aún no tiene resultado", error.Message);
    }

    // --- Game by game reporting ---------------------------------------------------------------------------

    [Fact]
    public void Games_can_be_recorded_one_at_a_time_as_they_finish()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(Magic());

        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);

        Assert.Equal(PairingStatus.AwaitingResult, pairing.Status);
        Assert.Equal(1, pairing.GamesWonBy(ana.Id));
    }

    [Fact]
    public void A_match_closes_itself_as_soon_as_it_is_decided()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(Magic());

        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);

        Assert.Equal(PairingStatus.Reported, pairing.Status);
        Assert.Equal(MatchResult.Win, pairing.ResultFor(ana.Id));
        Assert.Single(tournament.DomainEvents.OfType<ResultRecorded>());
    }

    [Fact]
    public void No_game_can_be_recorded_once_the_match_is_closed()
    {
        var (tournament, round, pairing, _, _) = MatchInProgress(OnePiece());
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);

        Assert.Throws<DomainException>(
            () => tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerB));
    }

    // --- Time expiry (ADR-0009) ---------------------------------------------------------------------------

    [Fact]
    public void Under_the_official_rule_whoever_is_ahead_takes_the_match_when_time_runs_out()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(Magic());
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(50));

        Assert.True(pairing.DecidedByTimeExpiry);
        Assert.Equal(MatchResult.Win, pairing.ResultFor(ana.Id));
        Assert.Equal(MatchResult.Loss, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void A_level_match_is_still_a_draw_when_time_runs_out()
    {
        var (tournament, round, pairing, ana, bruno) = MatchInProgress(Magic());
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerB);

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(50));

        Assert.Equal(MatchResult.Draw, pairing.ResultFor(ana.Id));
        Assert.Equal(MatchResult.Draw, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void A_match_with_no_games_played_is_a_draw_when_time_runs_out()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(Magic());

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(50));

        Assert.Equal(MatchResult.Draw, pairing.ResultFor(ana.Id));
    }

    [Fact]
    public void Under_the_always_draw_policy_the_leader_loses_their_advantage()
    {
        var tournament = Magic();
        tournament.SetTimeExpiryPolicy(TimeExpiryPolicy.AlwaysDraw);
        var (_, round, pairing, ana, bruno) = MatchInProgress(tournament);
        tournament.RecordGame(round.Id, pairing.Id, GameWinner.PlayerA);

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(50));

        Assert.Empty(pairing.Games);
        Assert.Equal(MatchResult.Draw, pairing.ResultFor(ana.Id));
        Assert.Equal(MatchResult.Draw, pairing.ResultFor(bruno.Id));
    }

    [Fact]
    public void Time_expiry_reports_which_matches_it_touched()
    {
        var (tournament, round, pairing, _, _) = MatchInProgress(OnePiece());

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(35));

        var raised = Assert.Single(tournament.DomainEvents.OfType<RoundTimeExpired>());
        Assert.Equal([pairing.Id], raised.AffectedPairingIds);
    }

    [Fact]
    public void Time_expiry_cannot_be_applied_while_overtime_is_still_running()
    {
        var (tournament, round, _, _, _) = MatchInProgress(OnePiece());

        // At 32 minutes regular time is gone, but the 5 minutes of overtime are still alive.
        var error = Assert.Throws<DomainException>(
            () => tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(32)));

        Assert.Contains("todavía tiene tiempo", error.Message);
    }

    [Fact]
    public void An_already_reported_result_is_not_overwritten_when_time_runs_out()
    {
        var (tournament, round, pairing, ana, _) = MatchInProgress(OnePiece());
        tournament.ReportResult(round.Id, pairing.Id, [GameWinner.PlayerA], ana.Id);

        tournament.ApplyTimeExpiry(round.Id, Now.AddMinutes(35));

        Assert.False(pairing.DecidedByTimeExpiry);
        Assert.Equal(MatchResult.Win, pairing.ResultFor(ana.Id));
    }

    [Fact]
    public void A_round_with_pending_results_cannot_be_closed()
    {
        var (tournament, round, _, _, _) = MatchInProgress(Magic());

        var error = Assert.Throws<DomainException>(() => tournament.CloseRound(round.Id, Now.AddMinutes(50)));

        Assert.Contains("resultados pendientes", error.Message);
    }

    private static (Tournament Tournament, Round Round, Pairing Pairing, Participant Ana, Participant Bruno)
        MatchInProgress(Tournament tournament)
    {
        tournament.Publish();
        var ana = tournament.SignUp("Ana");
        var bruno = tournament.SignUp("Bruno");
        var round = tournament.StartRound(Phase.Swiss, Now);
        var pairing = round.Pair(table: 1, ana.Id, bruno.Id);

        return (tournament, round, pairing, ana, bruno);
    }
}
