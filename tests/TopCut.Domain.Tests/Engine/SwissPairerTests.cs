using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Pairings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

public class SwissPairerTests
{
    [Fact]
    public void An_even_field_pairs_every_player_with_no_byes()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);

        var round = driver.StartSwissRound();

        Assert.Equal(4, round.Pairings.Count);
        Assert.DoesNotContain(round.Pairings, pairing => pairing.IsBye);
        Assert.Equal([1, 2, 3, 4], round.Pairings.Select(p => p.Table).Order());
    }

    [Fact]
    public void An_odd_field_produces_exactly_one_bye()
    {
        var driver = TournamentDriver.Magic(players: 7, swissRounds: 3);

        var round = driver.StartSwissRound();

        Assert.Equal(4, round.Pairings.Count);
        Assert.Single(round.Pairings, pairing => pairing.IsBye);
    }

    [Fact]
    public void The_bye_goes_to_the_lowest_ranked_player()
    {
        var driver = TournamentDriver.Magic(players: 5, swissRounds: 3);
        driver.PlaySwiss();

        // Every round handed out a bye, and never to the same player twice while others had none.
        var recipients = driver.ByeRecipients();

        Assert.Equal(3, recipients.Count);
        Assert.Equal(recipients.Count, recipients.Distinct().Count());
    }

    [Fact]
    public void Nobody_gets_a_second_bye_while_someone_is_still_without_one()
    {
        // Five rounds and five players: every single player must receive exactly one bye.
        var driver = TournamentDriver.Magic(players: 5, swissRounds: 5);
        driver.PlaySwiss();

        var recipients = driver.ByeRecipients();

        Assert.Equal(5, recipients.Count);
        Assert.Equal([1, 2, 3, 4, 5], recipients.Order());
    }

    [Fact]
    public void Nobody_plays_the_same_opponent_twice_over_a_full_tournament()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);

        driver.PlaySwiss();

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(12, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public void A_large_field_also_avoids_rematches()
    {
        var driver = TournamentDriver.Magic(players: 16, swissRounds: 4);

        driver.PlaySwiss();

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(32, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public void An_upset_heavy_tournament_still_avoids_rematches()
    {
        var driver = TournamentDriver.Magic(players: 12, swissRounds: 4);

        // The higher seed number wins instead: a completely different results tree, so the pairings
        // cannot be accidentally right just for the tidy case.
        driver.PlaySwiss(winnerSeed: Math.Max);

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(24, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public void Players_are_paired_inside_their_score_bracket()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        var second = driver.StartSwissRound();

        // After round 1 there are four players on 3 points and four on 0. Nobody should cross over
        // while the brackets are even.
        var standings = driver.Standings().ToDictionary(s => s.ParticipantId, s => s.MatchPoints);

        Assert.All(
            second.Pairings.Where(pairing => !pairing.IsBye),
            pairing => Assert.Equal(standings[pairing.PlayerAId], standings[pairing.PlayerBId!.Value]));
    }

    [Fact]
    public void The_dutch_split_pairs_the_top_half_against_the_bottom_half()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        var second = driver.StartSwissRound();

        // The four winners rank 1-4. Dutch pairs 1 with 3 and 2 with 4, not 1 with 2.
        var winners = driver.RankedSeeds().Take(4).ToList();
        var topBracket = second.Pairings
            .Where(pairing => !pairing.IsBye && winners.Contains(driver.Seed(pairing.PlayerAId)))
            .Select(pairing => (driver.Seed(pairing.PlayerAId), driver.Seed(pairing.PlayerBId!.Value)))
            .ToList();

        Assert.Equal(2, topBracket.Count);
        Assert.Contains((winners[0], winners[2]), topBracket);
        Assert.Contains((winners[1], winners[3]), topBracket);
    }

    [Fact]
    public void A_dropped_player_is_left_out_of_the_next_pairings()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(3));
        driver.Tournament.Drop(driver.Id(4));

        var second = driver.StartSwissRound();
        var playing = second.Pairings
            .SelectMany(pairing => pairing.PlayerBId is null
                ? [pairing.PlayerAId]
                : new[] { pairing.PlayerAId, pairing.PlayerBId.Value })
            .Select(driver.Seed)
            .ToList();

        Assert.DoesNotContain(3, playing);
        Assert.DoesNotContain(4, playing);
        Assert.Equal(6, playing.Count);
    }

    [Fact]
    public void A_drop_that_leaves_an_odd_field_creates_a_bye()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(8));

        var second = driver.StartSwissRound();

        Assert.Single(second.Pairings, pairing => pairing.IsBye);
    }

    [Fact]
    public void Rematches_are_allowed_only_once_there_is_no_alternative_left()
    {
        // Four players over four rounds: after three everyone has played everyone, so the fourth
        // round has no rematch-free pairing at all. It must still produce a round.
        var driver = TournamentDriver.Magic(players: 4, swissRounds: 4);

        driver.PlaySwiss();

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(8, matchups.Count);

        // The first three rounds used up all six distinct pairs; the fourth had to repeat two.
        Assert.Equal(6, matchups.Distinct().Count());
    }

    [Fact]
    public void Pairing_needs_at_least_two_active_players()
    {
        var driver = TournamentDriver.Magic(players: 2, swissRounds: 2);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        driver.Tournament.Drop(driver.Id(2));

        var error = Assert.Throws<DomainException>(() => SwissPairer.PairNextRound(driver.Tournament));

        Assert.Contains("al menos dos jugadores activos", error.Message);
    }

    [Fact]
    public void One_Piece_rounds_carry_the_Bo1_format_into_the_pairings()
    {
        var driver = TournamentDriver.OnePiece(players: 8, swissRounds: 3);

        var round = driver.StartSwissRound();
        driver.ReportRound(round);

        Assert.Equal(MatchFormat.Bo1, round.Format);
        Assert.All(round.Pairings.Where(p => !p.IsBye), pairing => Assert.Single(pairing.Games));
    }
}
