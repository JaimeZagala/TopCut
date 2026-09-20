using TopCut.Domain.Common;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Pairings;

/// <summary>A pairing the engine decided on. A null opponent is a bye.</summary>
public sealed record ProposedPairing(int Table, Guid PlayerAId, Guid? PlayerBId);

/// <summary>
/// Swiss pairing, Dutch variant: players are ranked, split into score brackets, and inside each
/// bracket the top half plays the bottom half. It is the standard in Magic, One Piece, Pokemon and
/// essentially every organised TCG, so there is nothing to invent here.
/// </summary>
public static class SwissPairer
{
    /// <summary>
    /// Works out the next round's pairings without touching the tournament. Keeping it a pure
    /// function is what makes it cheap to test against whole simulated tournaments.
    /// </summary>
    public static IReadOnlyList<ProposedPairing> PairNextRound(Tournament tournament)
    {
        var standings = StandingsCalculator.For(tournament);
        var active = tournament.ActiveParticipants.Select(participant => participant.Id).ToHashSet();

        DomainException.Require(active.Count >= 2, "Hacen falta al menos dos jugadores activos para emparejar.");

        // Ranked order is the backbone of the whole algorithm: brackets, the bye and the Dutch
        // split are all defined in terms of it.
        var ranked = standings
            .Where(standing => active.Contains(standing.ParticipantId))
            .OrderBy(standing => standing.Rank)
            .ToList();

        var alreadyPlayed = PreviousOpponents(tournament);
        var pairings = new List<ProposedPairing>();

        Guid? receivesBye = null;

        if (ranked.Count % 2 == 1)
        {
            receivesBye = ChooseBye(ranked, tournament);
            ranked.RemoveAll(standing => standing.ParticipantId == receivesBye);
        }

        var ids = ranked.Select(standing => standing.ParticipantId).ToList();
        var points = ranked.ToDictionary(s => s.ParticipantId, s => s.MatchPoints);

        // First pass refuses rematches outright. Only if no valid pairing exists at all does the
        // second pass allow them, which is what "avoid rematches while alternatives exist" means.
        var matched = Match(ids, points, alreadyPlayed, allowRematches: false)
            ?? Match(ids, points, alreadyPlayed, allowRematches: true)
            ?? throw new DomainException("No se pudo emparejar la ronda.");

        var table = 1;

        foreach (var (a, b) in matched)
        {
            pairings.Add(new ProposedPairing(table++, a, b));
        }

        if (receivesBye is { } bye)
        {
            // Last table: it is the one with nobody sitting at it.
            pairings.Add(new ProposedPairing(table, bye, null));
        }

        return pairings;
    }

    /// <summary>
    /// The bye goes to the lowest-ranked player who has not had one yet. Giving a second bye while
    /// someone is still on zero would hand out free points unevenly.
    /// </summary>
    private static Guid ChooseBye(List<Standing> ranked, Tournament tournament)
    {
        var byesSoFar = ByeCounts(tournament);

        return ranked
            .OrderBy(standing => byesSoFar.GetValueOrDefault(standing.ParticipantId))
            .ThenByDescending(standing => standing.Rank)
            .First()
            .ParticipantId;
    }

    private static Dictionary<Guid, int> ByeCounts(Tournament tournament)
    {
        var counts = new Dictionary<Guid, int>();

        foreach (var pairing in tournament.Rounds.SelectMany(round => round.Pairings).Where(p => p.IsBye))
        {
            counts[pairing.PlayerAId] = counts.GetValueOrDefault(pairing.PlayerAId) + 1;
        }

        return counts;
    }

    private static HashSet<(Guid, Guid)> PreviousOpponents(Tournament tournament)
    {
        var played = new HashSet<(Guid, Guid)>();

        foreach (var pairing in tournament.Rounds.SelectMany(round => round.Pairings))
        {
            if (pairing.PlayerBId is { } opponent)
            {
                played.Add(Key(pairing.PlayerAId, opponent));
            }
        }

        return played;
    }

    private static (Guid, Guid) Key(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>
    /// Pairs the ranked list by backtracking. The candidate order is what makes the result Dutch:
    /// the preferred opponent is the matching player in the other half of the score bracket, and
    /// alternatives are tried outwards from there. Backtracking only kicks in when that preference
    /// leads to a dead end, so a valid pairing is found whenever one exists.
    /// </summary>
    private static List<(Guid A, Guid B)>? Match(
        List<Guid> remaining,
        Dictionary<Guid, int> points,
        HashSet<(Guid, Guid)> played,
        bool allowRematches)
    {
        if (remaining.Count == 0)
        {
            return [];
        }

        var first = remaining[0];

        foreach (var index in CandidateOrder(remaining, points))
        {
            var candidate = remaining[index];

            if (!allowRematches && played.Contains(Key(first, candidate)))
            {
                continue;
            }

            var rest = new List<Guid>(remaining);
            rest.RemoveAt(index);
            rest.RemoveAt(0);

            if (Match(rest, points, played, allowRematches) is { } tail)
            {
                tail.Insert(0, (first, candidate));
                return tail;
            }
        }

        return null;
    }

    /// <summary>
    /// Indexes into <paramref name="remaining"/>, best opponent first, for the player at index 0.
    /// </summary>
    private static IEnumerable<int> CandidateOrder(List<Guid> remaining, Dictionary<Guid, int> points)
    {
        var topPoints = points[remaining[0]];

        // How many players share the leader's score. That group is the bracket being paired.
        var bracketSize = remaining.Count(id => points[id] == topPoints);

        // Dutch: the first of the top half meets the first of the bottom half.
        var preferred = Math.Min(Math.Max(bracketSize / 2, 1), remaining.Count - 1);

        return Enumerable.Range(1, remaining.Count - 1)
            .OrderBy(index => Math.Abs(index - preferred))
            // A tie in distance resolves downwards, so pair-downs come from the top of the next
            // bracket rather than from an arbitrary side.
            .ThenBy(index => index);
    }
}
