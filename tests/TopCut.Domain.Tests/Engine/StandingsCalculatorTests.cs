using TopCut.Domain.Catalog;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

/// <summary>
/// The tiebreaker arithmetic, checked against the worked examples in the official rulebooks.
/// Values are asserted exactly: "roughly right" standings decide who makes the top cut.
/// </summary>
public class StandingsCalculatorTests
{
    private static Standing Of(IReadOnlyList<Standing> standings, TournamentDriver driver, int seed) =>
        standings.Single(row => driver.Seed(row.ParticipantId) == seed);

    // --- Scoring -------------------------------------------------------------------------------

    [Fact]
    public void A_win_is_three_points_and_a_loss_is_none()
    {
        var driver = TournamentDriver.Magic(players: 2, swissRounds: 1);
        var round = driver.StartRoundWith((1, 2));
        driver.ReportMatch(round, winnerSeed: 1, loserSeed: 2);

        var standings = driver.Standings();

        Assert.Equal(3, Of(standings, driver, 1).MatchPoints);
        Assert.Equal(0, Of(standings, driver, 2).MatchPoints);
        Assert.Equal(1, Of(standings, driver, 1).Rank);
    }

    [Fact]
    public void A_draw_is_one_point_each()
    {
        var driver = TournamentDriver.Magic(players: 2, swissRounds: 1);
        var round = driver.StartRoundWith((1, 2));
        driver.Tournament.ReportResult(
            round.Id,
            round.Pairings[0].Id,
            [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.Draw],
            reportedBy: null);

        var standings = driver.Standings();

        Assert.Equal(1, Of(standings, driver, 1).MatchPoints);
        Assert.Equal(1, Of(standings, driver, 2).MatchPoints);
        Assert.Equal(1, Of(standings, driver, 1).Draws);
    }

    [Fact]
    public void A_bye_is_worth_a_win_in_both_games()
    {
        foreach (var driver in new[] { TournamentDriver.Magic(3, 1), TournamentDriver.OnePiece(3, 1) })
        {
            var round = driver.StartSwissRound();
            driver.ReportRound(round);

            var onABye = driver.Standings().Single(row => row.Byes == 1);

            Assert.Equal(3, onABye.MatchPoints);
        }
    }

    // --- Where the two games disagree ------------------------------------------------------------

    [Fact]
    public void Magic_counts_a_bye_towards_the_players_own_percentage()
    {
        var driver = TournamentDriver.Magic(players: 3, swissRounds: 1);
        var round = driver.StartSwissRound();
        driver.ReportRound(round);

        var onABye = driver.Standings().Single(row => row.Byes == 1);

        // 3 match points over one round worth 3: a perfect record, exactly as the MTR example says.
        Assert.Equal(1.0m, onABye.OwnMatchWinPercentage);
    }

    [Fact]
    public void One_Piece_leaves_byes_out_of_the_players_own_percentage()
    {
        var driver = TournamentDriver.OnePiece(players: 3, swissRounds: 1);
        var round = driver.StartSwissRound();
        driver.ReportRound(round);

        var onABye = driver.Standings().Single(row => row.Byes == 1);

        // Excluded from numerator and denominator alike, so the player has no played rounds yet and
        // falls to the floor. Same data, opposite result from Magic: this is why the ruleset is data.
        Assert.Equal(3, onABye.MatchPoints);
        Assert.Equal(0.33m, onABye.OwnMatchWinPercentage);
    }

    // --- The floor ---------------------------------------------------------------------------------

    [Fact]
    public void A_player_who_loses_everything_is_floored_at_033()
    {
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 2);
        driver.PlaySwiss();

        var last = driver.Standings().Single(row => row.Rank == 4);

        Assert.Equal(0, last.MatchPoints);
        Assert.Equal(0.33m, last.OwnMatchWinPercentage);
        Assert.Equal(0.33m, last.GameWinPercentage);
    }

    [Fact]
    public void The_floor_lifts_a_weak_opponent_when_averaging()
    {
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 2);
        var first = driver.StartRoundWith((1, 4), (2, 3));
        driver.ReportMatch(first, winnerSeed: 1, loserSeed: 4);
        driver.ReportMatch(first, winnerSeed: 2, loserSeed: 3);
        driver.CloseRound(first);

        var second = driver.StartRoundWith((1, 3), (2, 4));
        driver.ReportMatch(second, winnerSeed: 3, loserSeed: 1);
        driver.ReportMatch(second, winnerSeed: 2, loserSeed: 4);

        var standings = driver.Standings();

        // P4 lost both, so its real percentage is 0 and the floor lifts it to 0.33. P3 won one of
        // two, so 0.5. P1 faced exactly those two: the average is (0.33 + 0.5) / 2.
        Assert.Equal(0m, Of(standings, driver, 4).MatchPoints);
        Assert.Equal(0.33m, Of(standings, driver, 4).OwnMatchWinPercentage);
        Assert.Equal(0.5m, Of(standings, driver, 3).OwnMatchWinPercentage);
        Assert.Equal(0.415m, Of(standings, driver, 1).OpponentsMatchWinPercentage);
    }

    // --- Opponents' percentages ----------------------------------------------------------------------

    [Fact]
    public void A_bye_round_is_skipped_when_averaging_the_opponents()
    {
        var driver = TournamentDriver.Magic(players: 3, swissRounds: 2);

        // P3 sits out the first round, then plays P1 in the second.
        var first = driver.StartSwissRound();
        var satOut = first.Pairings.Single(pairing => pairing.IsBye).PlayerAId;
        driver.ReportRound(first);
        driver.CloseRound(first);

        var second = driver.StartSwissRound();
        driver.ReportRound(second);

        var standings = driver.Standings();
        var theirRow = standings.Single(row => row.ParticipantId == satOut);
        var theOpponent = standings.Single(row => driver.Seed(row.ParticipantId) == 1);

        // One bye and one real match, so the average is that single opponent's percentage and not
        // a phantom entry dragged in by the round nobody played.
        Assert.Equal(1, theirRow.Byes);
        Assert.Equal(theOpponent.OwnMatchWinPercentage, theirRow.OpponentsMatchWinPercentage);
    }

    // --- Game percentages -----------------------------------------------------------------------------

    [Fact]
    public void A_two_one_in_Bo3_feeds_the_game_percentages_from_the_individual_games()
    {
        var driver = TournamentDriver.Magic(players: 2, swissRounds: 1);
        var round = driver.StartRoundWith((1, 2));
        driver.ReportMatch(round, winnerSeed: 1, loserSeed: 2, gamesLostByWinner: 1);

        var standings = driver.Standings();

        // Three games played. The winner took two of them: 6 game points out of 9.
        Assert.Equal(Math.Round(6m / 9m, 6), Math.Round(Of(standings, driver, 1).GameWinPercentage, 6));
        Assert.Equal(Math.Round(3m / 9m, 6), Math.Round(Of(standings, driver, 2).GameWinPercentage, 6));
    }

    [Fact]
    public void A_clean_two_nil_beats_a_two_one_on_game_percentage_alone()
    {
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 1);
        var round = driver.StartRoundWith((1, 2), (3, 4));
        driver.ReportMatch(round, winnerSeed: 1, loserSeed: 2);
        driver.ReportMatch(round, winnerSeed: 3, loserSeed: 4, gamesLostByWinner: 1);

        var standings = driver.Standings();

        Assert.Equal(1.0m, Of(standings, driver, 1).GameWinPercentage);
        Assert.True(Of(standings, driver, 3).GameWinPercentage < 1.0m);
    }

    // --- The tiebreaker order is data ----------------------------------------------------------------

    [Fact]
    public void Magic_separates_two_level_players_by_opponents_match_win_percentage_first()
    {
        var driver = BuildTiedScenario(TournamentDriver.Magic(players: 4, swissRounds: 2));

        var standings = driver.Standings();
        var p1 = Of(standings, driver, 1);
        var p2 = Of(standings, driver, 2);

        Assert.Equal(p1.MatchPoints, p2.MatchPoints);
        Assert.True(p1.OpponentsMatchWinPercentage > p2.OpponentsMatchWinPercentage);
        Assert.True(p1.GameWinPercentage < p2.GameWinPercentage);

        // Magic applies OMW% before GW%, so the tougher schedule wins out.
        Assert.True(p1.Rank < p2.Rank);
    }

    [Fact]
    public void Reordering_the_tiebreakers_flips_the_same_result()
    {
        var driver = BuildTiedScenario(TournamentDriver.MagicWithTiebreakers(
            players: 4,
            swissRounds: 2,
            Tiebreaker.GameWinPercentage,
            Tiebreaker.OpponentsMatchWinPercentage));

        var standings = driver.Standings();

        // Identical results, identical numbers, opposite ranking: the order really does come from
        // the ruleset and is not baked into the calculator.
        Assert.True(Of(standings, driver, 2).Rank < Of(standings, driver, 1).Rank);
    }

    /// <summary>
    /// Leaves P1 and P2 level on 3 points, with P1 ahead on strength of schedule and P2 ahead on
    /// games won. Whichever of the two criteria the ruleset applies first decides who is above.
    /// </summary>
    private static TournamentDriver BuildTiedScenario(TournamentDriver driver)
    {
        var first = driver.StartRoundWith((1, 2), (3, 4));
        driver.ReportMatch(first, winnerSeed: 1, loserSeed: 2, gamesLostByWinner: 1);
        driver.ReportMatch(first, winnerSeed: 3, loserSeed: 4);
        driver.CloseRound(first);

        var second = driver.StartRoundWith((1, 3), (2, 4));
        driver.ReportMatch(second, winnerSeed: 3, loserSeed: 1);
        driver.ReportMatch(second, winnerSeed: 2, loserSeed: 4);

        return driver;
    }

    // --- Head to head -----------------------------------------------------------------------------------

    [Fact]
    public void One_Piece_falls_back_to_the_match_the_two_of_them_played()
    {
        var driver = TournamentDriver.OnePiece(players: 4, swissRounds: 3);

        var first = driver.StartRoundWith((1, 2), (3, 4));
        driver.ReportMatch(first, winnerSeed: 2, loserSeed: 1);
        driver.ReportMatch(first, winnerSeed: 3, loserSeed: 4);
        driver.CloseRound(first);

        var second = driver.StartRoundWith((1, 3), (2, 4));
        driver.ReportMatch(second, winnerSeed: 1, loserSeed: 3);
        driver.ReportMatch(second, winnerSeed: 4, loserSeed: 2);
        driver.CloseRound(second);

        var third = driver.StartRoundWith((1, 4), (2, 3));
        driver.ReportMatch(third, winnerSeed: 1, loserSeed: 4);
        driver.ReportMatch(third, winnerSeed: 2, loserSeed: 3);

        var standings = driver.Standings();
        var p1 = Of(standings, driver, 1);
        var p2 = Of(standings, driver, 2);

        // Both finish 2-1 having faced the same three opponents, so every percentage matches to the
        // decimal. P2 beat P1 in round one, and One Piece says that is what settles it.
        Assert.Equal(p1.MatchPoints, p2.MatchPoints);
        Assert.Equal(p1.OwnMatchWinPercentage, p2.OwnMatchWinPercentage);
        Assert.Equal(p1.OpponentsMatchWinPercentage, p2.OpponentsMatchWinPercentage);
        Assert.True(p2.Rank < p1.Rank);
    }

    // --- Drops ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_player_who_drops_keeps_the_results_they_already_earned()
    {
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 2);
        var first = driver.StartRoundWith((1, 2), (3, 4));
        driver.ReportMatch(first, winnerSeed: 1, loserSeed: 2);
        driver.ReportMatch(first, winnerSeed: 3, loserSeed: 4);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(1));

        var standings = driver.Standings();
        var dropped = Of(standings, driver, 1);

        Assert.True(dropped.Dropped);
        Assert.Equal(3, dropped.MatchPoints);
        Assert.Equal(1, dropped.Wins);
    }

    [Fact]
    public void A_dropped_opponent_still_counts_towards_everyone_elses_tiebreakers()
    {
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 2);
        var first = driver.StartRoundWith((1, 2), (3, 4));
        driver.ReportMatch(first, winnerSeed: 1, loserSeed: 2);
        driver.ReportMatch(first, winnerSeed: 3, loserSeed: 4);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(1));

        var standings = driver.Standings();

        // P2 played P1 and nobody else. Their strength of schedule is P1's percentage, withdrawal
        // or not: erasing it would rewrite the tournament that was actually played.
        Assert.Equal(
            Of(standings, driver, 1).OwnMatchWinPercentage,
            Of(standings, driver, 2).OpponentsMatchWinPercentage);
    }

    // --- Ranking ---------------------------------------------------------------------------------------------

    [Fact]
    public void Ranks_are_consecutive_and_start_at_one()
    {
        var driver = TournamentDriver.Magic(players: 9, swissRounds: 3);
        driver.PlaySwiss();

        var standings = driver.Standings();

        Assert.Equal(Enumerable.Range(1, 9), standings.Select(row => row.Rank).Order());
    }

    [Fact]
    public void Match_points_always_outrank_any_tiebreaker()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        driver.PlaySwiss();

        var ordered = driver.Standings().OrderBy(row => row.Rank).ToList();

        Assert.Equal(
            ordered.Select(row => row.MatchPoints),
            ordered.Select(row => row.MatchPoints).OrderDescending());
    }

    [Fact]
    public void The_record_reads_the_way_players_read_it()
    {
        var driver = TournamentDriver.Magic(players: 3, swissRounds: 2);
        driver.PlaySwiss();

        var leader = driver.Standings().Single(row => row.Rank == 1);

        Assert.Equal(2, leader.RoundsPlayed);
        Assert.Equal("2-0-0", leader.Record);
    }
}
