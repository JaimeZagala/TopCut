using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Pairings;

/// <summary>
/// Single-elimination bracket after the Swiss phase. Optional: a tournament can be Swiss all the
/// way through, which is what <c>TopCutSize</c> being null means.
/// </summary>
public static class TopCutBracket
{
    /// <summary>
    /// Seeds the cut from the final Swiss standings: 1 plays the last qualifier, 2 plays the second
    /// to last, and so on, which is the method the Magic Tournament Rules describe. Seeding this
    /// way is the reward for finishing high — the best record faces the weakest qualifier.
    /// </summary>
    public static IReadOnlyList<ProposedPairing> FirstRound(Tournament tournament)
    {
        DomainException.Require(tournament.HasTopCut, "Este torneo no tiene top cut configurado.");
        DomainException.Require(
            tournament.Rounds.Count(round => round.Phase == Phase.Swiss) == tournament.SwissRounds,
            "Todavia quedan rondas suizas por jugar.");

        var size = tournament.TopCutSize!.Value;

        var qualifiers = StandingsCalculator.For(tournament)
            .Where(standing => !standing.Dropped)
            .OrderBy(standing => standing.Rank)
            .Take(size)
            .ToList();

        DomainException.Require(
            qualifiers.Count == size,
            $"No hay {size} jugadores para el top cut: solo quedan {qualifiers.Count} sin retirarse.");

        return Seed([.. qualifiers.Select(standing => standing.ParticipantId)]);
    }

    /// <summary>
    /// The next elimination round, built from who won the previous one. Bracket order is preserved,
    /// so the winners of tables 1 and 2 meet, then 3 and 4, and the bracket narrows as drawn.
    /// </summary>
    public static IReadOnlyList<ProposedPairing> NextRound(Round previous)
    {
        DomainException.Require(previous.Phase == Phase.TopCut, "Esa ronda no es de top cut.");
        DomainException.Require(previous.AllResultsReported, "La ronda anterior tiene resultados pendientes.");

        var winners = previous.Pairings
            .OrderBy(pairing => pairing.Table)
            .Select(Winner)
            .ToList();

        DomainException.Require(winners.Count > 1, "El top cut ya tiene ganador.");

        var pairings = new List<ProposedPairing>();

        for (var i = 0; i < winners.Count; i += 2)
        {
            pairings.Add(new ProposedPairing((i / 2) + 1, winners[i], winners[i + 1]));
        }

        return pairings;
    }

    /// <summary>Whether the bracket is down to its last match.</summary>
    public static bool IsFinal(Round round) => round.Phase == Phase.TopCut && round.Pairings.Count == 1;

    private static Guid Winner(Pairing pairing)
    {
        if (pairing.IsBye)
        {
            return pairing.PlayerAId;
        }

        // Draws are not allowed in elimination rounds, and both rulebooks say so explicitly. If one
        // reaches here the round was mis-reported, and guessing a winner would be worse than saying so.
        DomainException.Require(
            pairing.PlayerAWins != pairing.PlayerBWins,
            $"La mesa {pairing.Table} del top cut quedo en empate, y la eliminacion directa no los admite.");

        return pairing.PlayerAWins > pairing.PlayerBWins ? pairing.PlayerAId : pairing.PlayerBId!.Value;
    }

    /// <summary>Pairs the seeds 1-vs-last, 2-vs-second-to-last, and so on down the middle.</summary>
    private static List<ProposedPairing> Seed(List<Guid> qualifiers)
    {
        var pairings = new List<ProposedPairing>();
        var table = 1;

        for (var top = 0; top < qualifiers.Count / 2; top++)
        {
            var bottom = qualifiers.Count - 1 - top;
            pairings.Add(new ProposedPairing(table++, qualifiers[top], qualifiers[bottom]));
        }

        return pairings;
    }
}
