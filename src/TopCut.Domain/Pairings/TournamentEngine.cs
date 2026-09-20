using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Pairings;

/// <summary>
/// The one entry point the application needs to advance a tournament: it works out which phase is
/// next, asks the right pairer for the matchups and lays the round out. Everything the organiser
/// does from the live screen goes through here.
/// </summary>
public static class TournamentEngine
{
    /// <summary>
    /// Opens the next round: Swiss while Swiss rounds remain, then the bracket if there is one.
    /// </summary>
    public static Round StartNextRound(Tournament tournament, DateTimeOffset now)
    {
        var phase = NextPhase(tournament);

        var proposed = phase == Phase.Swiss
            ? SwissPairer.PairNextRound(tournament)
            : NextBracketRound(tournament);

        var round = tournament.StartRound(phase, now);

        foreach (var pairing in proposed)
        {
            round.Pair(pairing.Table, pairing.PlayerAId, pairing.PlayerBId);
        }

        return round;
    }

    /// <summary>Whether there is anything left to play.</summary>
    public static bool HasNextRound(Tournament tournament) => TryNextPhase(tournament) is not null;

    /// <summary>
    /// Which phase the next round belongs to. Throws when the tournament has run its course, since
    /// there is no sensible answer and returning one would hide a bug in the caller.
    /// </summary>
    public static Phase NextPhase(Tournament tournament) =>
        TryNextPhase(tournament) ?? throw new DomainException("El torneo ya ha jugado todas sus rondas.");

    private static Phase? TryNextPhase(Tournament tournament)
    {
        if (tournament.Rounds.Count(round => round.Phase == Phase.Swiss) < tournament.SwissRounds)
        {
            return Phase.Swiss;
        }

        if (!tournament.HasTopCut)
        {
            return null;
        }

        var lastCut = LastBracketRound(tournament);

        // The bracket is done when the last round was a single match: that was the final.
        return lastCut is null || lastCut.Pairings.Count > 1 ? Phase.TopCut : null;
    }

    private static IReadOnlyList<ProposedPairing> NextBracketRound(Tournament tournament) =>
        LastBracketRound(tournament) is { } previous
            ? TopCutBracket.NextRound(previous)
            : TopCutBracket.FirstRound(tournament);

    private static Round? LastBracketRound(Tournament tournament) =>
        tournament.Rounds.LastOrDefault(round => round.Phase == Phase.TopCut);
}
