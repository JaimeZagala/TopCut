using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Pairings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

public class TopCutBracketTests
{
    private static (int A, int B) Seeds(TournamentDriver driver, Pairing pairing) =>
        (driver.Seed(pairing.PlayerAId), driver.Seed(pairing.PlayerBId!.Value));

    [Fact]
    public void A_top_eight_seeds_first_against_eighth_and_works_inwards()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 8);
        driver.PlaySwiss();
        var ranked = driver.RankedSeeds();

        var cut = driver.StartTopCutRound();
        var tables = cut.Pairings.OrderBy(p => p.Table).Select(p => Seeds(driver, p)).ToList();

        Assert.Equal(4, tables.Count);
        Assert.Equal((ranked[0], ranked[7]), tables[0]);
        Assert.Equal((ranked[1], ranked[6]), tables[1]);
        Assert.Equal((ranked[2], ranked[5]), tables[2]);
        Assert.Equal((ranked[3], ranked[4]), tables[3]);
    }

    [Fact]
    public void A_top_four_takes_only_the_best_four()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();
        var ranked = driver.RankedSeeds();

        var cut = driver.StartTopCutRound();
        var playing = cut.Pairings.SelectMany(p => new[] { driver.Seed(p.PlayerAId), driver.Seed(p.PlayerBId!.Value) });

        Assert.Equal(2, cut.Pairings.Count);
        Assert.Equal(ranked.Take(4).Order(), playing.Order());
    }

    [Fact]
    public void The_bracket_narrows_by_pairing_the_winners_in_order()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();

        var semis = driver.StartTopCutRound();
        var expected = semis.Pairings.OrderBy(p => p.Table)
            .Select(p => Math.Min(driver.Seed(p.PlayerAId), driver.Seed(p.PlayerBId!.Value)))
            .ToList();
        driver.ReportRound(semis);
        driver.CloseRound(semis);

        var final = driver.StartTopCutRound();

        Assert.Single(final.Pairings);
        Assert.True(TopCutBracket.IsFinal(final));
        Assert.Equal((expected[0], expected[1]), Seeds(driver, final.Pairings[0]));
    }

    [Fact]
    public void A_top_cut_round_carries_its_own_format_and_clock()
    {
        var driver = TournamentDriver.OnePiece(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();

        var cut = driver.StartTopCutRound();

        // One Piece plays its Swiss as a single game and its cut best of three, with a longer clock.
        Assert.Equal(MatchFormat.Bo1, driver.Tournament.Rounds.First().Format);
        Assert.Equal(MatchFormat.Bo3, cut.Format);
        Assert.Equal(TimeSpan.FromMinutes(60), cut.RegularTimeEndsAt - cut.StartedAt);
    }

    [Fact]
    public void The_cut_cannot_start_while_swiss_rounds_remain()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        var first = driver.StartSwissRound();
        driver.ReportRound(first);
        driver.CloseRound(first);

        var error = Assert.Throws<DomainException>(() => TopCutBracket.FirstRound(driver.Tournament));

        Assert.Contains("rondas suizas", error.Message);
    }

    [Fact]
    public void A_tournament_without_a_top_cut_has_no_bracket_to_build()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3);
        driver.PlaySwiss();

        var error = Assert.Throws<DomainException>(() => TopCutBracket.FirstRound(driver.Tournament));

        Assert.Contains("no tiene top cut", error.Message);
    }

    [Fact]
    public void Too_many_withdrawals_stop_the_cut_rather_than_shrinking_it_silently()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 8);
        driver.PlaySwiss();

        driver.Tournament.Drop(driver.Id(7));
        driver.Tournament.Drop(driver.Id(8));

        var error = Assert.Throws<DomainException>(() => TopCutBracket.FirstRound(driver.Tournament));

        Assert.Contains("No hay 8 jugadores", error.Message);
    }

    [Fact]
    public void Withdrawn_players_never_take_a_spot_in_the_cut()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();

        // The Swiss winner withdraws before the cut: the next four qualify instead.
        var winner = driver.RankedSeeds()[0];
        driver.Tournament.Drop(driver.Id(winner));

        var cut = driver.StartTopCutRound();
        var playing = cut.Pairings
            .SelectMany(p => new[] { driver.Seed(p.PlayerAId), driver.Seed(p.PlayerBId!.Value) })
            .ToList();

        Assert.DoesNotContain(winner, playing);
        Assert.Equal(4, playing.Count);
    }

    [Fact]
    public void A_draw_in_the_bracket_is_refused_instead_of_being_guessed()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();

        var semis = driver.StartTopCutRound();

        foreach (var pairing in semis.Pairings)
        {
            driver.Tournament.ReportResult(
                semis.Id,
                pairing.Id,
                [GameWinner.PlayerA, GameWinner.PlayerB, GameWinner.Draw],
                reportedBy: null);
        }

        var error = Assert.Throws<DomainException>(() => TopCutBracket.NextRound(semis));

        // Elimination rounds admit no draws in either rulebook, so the engine says so rather than
        // inventing a winner.
        Assert.Contains("empate", error.Message);
    }

    [Fact]
    public void The_next_bracket_round_needs_the_previous_one_finished()
    {
        var driver = TournamentDriver.Magic(players: 8, swissRounds: 3, topCut: 4);
        driver.PlaySwiss();
        var semis = driver.StartTopCutRound();

        var error = Assert.Throws<DomainException>(() => TopCutBracket.NextRound(semis));

        Assert.Contains("resultados pendientes", error.Message);
    }
}
