using System.Diagnostics;
using TopCut.Domain.Tournaments;
using Xunit.Abstractions;

namespace TopCut.Domain.Tests.Engine;

/// <summary>
/// Field sizes from a four-player Friday night up to a hall of 128, with the awkward numbers in
/// between. The pairer backtracks, so the interesting risk is not the big field: it is the small
/// one played over many rounds, where nearly every possible matchup has already been used and the
/// search has to work for its answer.
/// </summary>
public class FieldSizeTests(ITestOutputHelper output)
{
    /// <summary>Rounds recommended by the Magic Tournament Rules for a given attendance.</summary>
    private static int RecommendedRounds(int players) => players switch
    {
        <= 8 => 3,
        <= 16 => 5,
        <= 32 => 5,
        <= 64 => 6,
        <= 128 => 7,
        _ => 8
    };

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(19)]
    [InlineData(23)]
    [InlineData(27)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(37)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(101)]
    [InlineData(128)]
    public void A_field_of_any_size_pairs_cleanly_for_its_recommended_rounds(int players)
    {
        var rounds = RecommendedRounds(players);
        var driver = TournamentDriver.Magic(players, rounds);

        var stopwatch = Stopwatch.StartNew();
        driver.PlaySwiss();
        stopwatch.Stop();

        output.WriteLine($"{players,4} jugadores x {rounds} rondas -> {stopwatch.ElapsedMilliseconds,5} ms");

        var standings = driver.Standings();
        Assert.Equal(players, standings.Count);
        Assert.Equal(Enumerable.Range(1, players), standings.Select(row => row.Rank).Order());

        // Everyone played every round, byes included.
        Assert.All(standings, row => Assert.Equal(rounds, row.RoundsPlayed));

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(matchups.Count, matchups.Distinct().Count());

        // An odd field gives out exactly one bye per round, and never twice to the same player
        // while anyone is still without one.
        var byes = driver.ByeRecipients();
        Assert.Equal(players % 2 == 1 ? rounds : 0, byes.Count);
        Assert.Equal(byes.Count, byes.Distinct().Count());
    }

    /// <summary>
    /// The hard case. With n players over n-1 rounds the only rematch-free schedule is a full round
    /// robin, so there is exactly one family of valid answers and the search cannot stumble onto it.
    /// </summary>
    [Theory]
    [InlineData(4, 3)]
    [InlineData(6, 5)]
    [InlineData(8, 7)]
    [InlineData(10, 9)]
    public void A_small_field_played_to_exhaustion_still_finds_the_round_robin(int players, int rounds)
    {
        var driver = TournamentDriver.Magic(players, rounds);

        var stopwatch = Stopwatch.StartNew();
        driver.PlaySwiss();
        stopwatch.Stop();

        output.WriteLine($"{players,4} jugadores x {rounds} rondas (round robin) -> {stopwatch.ElapsedMilliseconds,5} ms");

        var matchups = driver.PlayedMatchups().ToList();

        // Every pair exactly once: the complete round robin, with no repeats anywhere.
        Assert.Equal(players * (players - 1) / 2, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Fact]
    public void An_odd_field_played_to_exhaustion_gives_everyone_exactly_one_bye()
    {
        // Seven players over seven rounds: each sits out once and plays the other six.
        var driver = TournamentDriver.Magic(players: 7, swissRounds: 7);

        driver.PlaySwiss();

        var byes = driver.ByeRecipients();
        Assert.Equal(7, byes.Count);
        Assert.Equal(Enumerable.Range(1, 7), byes.Order());

        var matchups = driver.PlayedMatchups().ToList();
        Assert.Equal(21, matchups.Count);
        Assert.Equal(matchups.Count, matchups.Distinct().Count());
    }

    [Theory]
    [InlineData(19, 8)]
    [InlineData(27, 8)]
    [InlineData(37, 16)]
    [InlineData(53, 8)]
    public void A_top_cut_works_on_a_field_that_is_not_a_power_of_two(int players, int cutSize)
    {
        var rounds = RecommendedRounds(players);
        var driver = TournamentDriver.Magic(players, rounds, topCut: cutSize);

        driver.PlaySwiss();

        var qualifiers = driver.RankedSeeds().Take(cutSize).ToList();
        var first = driver.StartNextRound();

        // The bracket size has nothing to do with the attendance: 19 players can play a top 8.
        Assert.Equal(cutSize / 2, first.Pairings.Count);
        Assert.Equal(
            qualifiers.Order(),
            first.Pairings.SelectMany(p => new[] { driver.Seed(p.PlayerAId), driver.Seed(p.PlayerBId!.Value) }).Order());

        // And it narrows to a single winner.
        var round = first;

        while (round.Pairings.Count > 1)
        {
            driver.ReportRound(round);
            driver.CloseRound(round);
            round = driver.StartNextRound();
        }

        driver.ReportRound(round);
        driver.CloseRound(round);
        driver.Tournament.Complete();

        Assert.Equal(TournamentStatus.Completed, driver.Tournament.Status);
    }
}
