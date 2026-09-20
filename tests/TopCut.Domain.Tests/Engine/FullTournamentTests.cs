using TopCut.Domain.Catalog;
using TopCut.Domain.Pairings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

/// <summary>
/// Whole tournaments run start to finish. The individual pieces are tested elsewhere; what these
/// check is that they still hold together over eight rounds of real pairings, real standings and
/// real withdrawals — which is the only shape in which the engine will ever actually be used.
/// </summary>
public class FullTournamentTests
{
    [Theory]
    [InlineData(4, 2)]
    [InlineData(7, 3)]
    [InlineData(8, 3)]
    [InlineData(11, 4)]
    [InlineData(16, 4)]
    [InlineData(23, 5)]
    public void A_swiss_tournament_of_any_size_finishes_cleanly(int players, int rounds)
    {
        var driver = TournamentDriver.Magic(players, rounds);

        driver.PlaySwiss();
        driver.Tournament.Complete();

        var standings = driver.Standings();

        Assert.Equal(TournamentStatus.Completed, driver.Tournament.Status);
        Assert.Equal(players, standings.Count);
        Assert.Equal(Enumerable.Range(1, players), standings.Select(row => row.Rank).Order());

        // Everyone played every round, byes included. Nobody was quietly left out.
        Assert.All(standings, row => Assert.Equal(rounds, row.RoundsPlayed));

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Theory]
    [InlineData(9, 3)]
    [InlineData(15, 4)]
    public void An_odd_field_hands_out_one_bye_per_round_and_never_twice_to_the_same_player(int players, int rounds)
    {
        var driver = TournamentDriver.Magic(players, rounds);

        driver.PlaySwiss();

        var recipients = driver.ByeRecipients();

        Assert.Equal(rounds, recipients.Count);
        Assert.Equal(recipients.Count, recipients.Distinct().Count());
    }

    [Fact]
    public void A_tournament_survives_players_withdrawing_halfway_through()
    {
        var driver = TournamentDriver.Magic(players: 12, swissRounds: 4);

        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(5));
        driver.Tournament.Drop(driver.Id(9));

        var second = driver.StartSwissRound();
        driver.ReportRound(second);
        driver.CloseRound(second);

        driver.Tournament.Drop(driver.Id(11));

        for (var i = 0; i < 2; i++)
        {
            var round = driver.StartSwissRound();
            driver.ReportRound(round);
            driver.CloseRound(round);
        }

        driver.Tournament.Complete();
        var standings = driver.Standings();

        // Everyone is still listed, withdrawals included, with the results they earned before going.
        Assert.Equal(12, standings.Count);
        Assert.Equal(3, standings.Count(row => row.Dropped));
        Assert.All(standings.Where(row => row.Dropped), row => Assert.True(row.RoundsPlayed >= 1));

        // And the ones who stayed played every round.
        Assert.All(standings.Where(row => !row.Dropped), row => Assert.Equal(4, row.RoundsPlayed));

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public void An_odd_number_of_withdrawals_starts_producing_byes()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 4);

        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        Assert.Empty(driver.ByeRecipients());

        driver.Tournament.Drop(driver.Id(8));

        var second = driver.StartSwissRound();
        driver.ReportRound(second);
        driver.CloseRound(second);

        // Seven players left, so from here on every round has a bye.
        Assert.Single(driver.ByeRecipients());
    }

    [Fact]
    public void Swiss_plus_a_top_eight_runs_all_the_way_to_a_single_winner()
    {
        var driver = TournamentDriver.Magic(players: 16, swissRounds: 4, topCut: 8);

        driver.PlaySwiss();

        // Quarter-finals, semi-finals, final.
        Round? last = null;

        for (var i = 0; i < 3; i++)
        {
            last = driver.StartTopCutRound();
            driver.ReportRound(last);
            driver.CloseRound(last);
        }

        driver.Tournament.Complete();

        Assert.NotNull(last);
        Assert.True(TopCutBracket.IsFinal(last));
        Assert.Equal(7, driver.Tournament.Rounds.Count);
        Assert.Equal(3, driver.Tournament.Rounds.Count(round => round.Phase == Phase.TopCut));

        var champion = last.Pairings[0];
        Assert.Equal(TournamentStatus.Completed, driver.Tournament.Status);
        Assert.NotEqual(champion.PlayerAWins, champion.PlayerBWins);
    }

    [Fact]
    public void One_Piece_runs_its_swiss_as_Bo1_and_its_cut_as_Bo3()
    {
        var driver = TournamentDriver.OnePiece(players: 8, swissRounds: 3, topCut: 4);

        driver.PlaySwiss();

        var semis = driver.StartTopCutRound();
        driver.ReportRound(semis);
        driver.CloseRound(semis);

        var final = driver.StartTopCutRound();
        driver.ReportRound(final);
        driver.CloseRound(final);

        driver.Tournament.Complete();

        var swissRounds = driver.Tournament.Rounds.Where(round => round.Phase == Phase.Swiss).ToList();
        var cutRounds = driver.Tournament.Rounds.Where(round => round.Phase == Phase.TopCut).ToList();

        Assert.All(swissRounds, round => Assert.Equal(MatchFormat.Bo1, round.Format));
        Assert.All(swissRounds, round => Assert.All(
            round.Pairings.Where(p => !p.IsBye),
            pairing => Assert.Single(pairing.Games)));

        Assert.All(cutRounds, round => Assert.Equal(MatchFormat.Bo3, round.Format));
        Assert.All(cutRounds, round => Assert.All(
            round.Pairings,
            pairing => Assert.Equal(2, pairing.Games.Count)));
    }

    [Fact]
    public void A_tournament_played_entirely_in_swiss_needs_no_bracket()
    {
        var driver = TournamentDriver.Magic(players: 10, swissRounds: 4);

        driver.PlaySwiss();
        driver.Tournament.Complete();

        Assert.False(driver.Tournament.HasTopCut);
        Assert.All(driver.Tournament.Rounds, round => Assert.Equal(Phase.Swiss, round.Phase));
        Assert.Equal(1, driver.Standings().Count(row => row.Rank == 1));
    }

    [Fact]
    public void The_round_clock_running_out_settles_the_stragglers_and_the_round_still_closes()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 2);

        var first = driver.StartSwissRound();

        // Only two of the four tables report in time.
        foreach (var pairing in first.Pairings.Take(2))
        {
            driver.Tournament.ReportResult(
                first.Id,
                pairing.Id,
                [GameWinner.PlayerA, GameWinner.PlayerA],
                reportedBy: null);
        }

        driver.Tournament.ApplyTimeExpiry(first.Id, first.ExtraTimeEndsAt!.Value);
        driver.CloseRound(first);

        // Nothing is left hanging, and the tournament carries straight on into the next round.
        Assert.True(first.AllResultsReported);
        Assert.Equal(2, first.Pairings.Count(pairing => pairing.DecidedByTimeExpiry));

        var second = driver.StartSwissRound();
        Assert.Equal(4, second.Pairings.Count);
    }
}
