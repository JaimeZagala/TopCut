using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Standings;

/// <summary>
/// One player's season. Keyed on the store's book entry (ADR-0035), which is the only thing that
/// survives from one tournament to the next: the participant row does not, and the name is a
/// snapshot that two people can share.
/// </summary>
/// <param name="Tournaments">Of the league's, the ones where they actually played a round.</param>
/// <param name="Name">The last one they signed up with, because that is the one the room used.</param>
public sealed record LeagueStanding(
    Guid StorePlayerId,
    string Name,
    int Rank,
    int Points,
    int Tournaments,
    int Wins,
    int Losses,
    int Draws,
    int Byes)
{
    /// <summary>The whole season as one W-L-D line, byes folded into wins as players read them.</summary>
    public string Record => $"{Wins + Byes}-{Losses}-{Draws}";
}

/// <summary>
/// A league's cumulative table: the same scoring model as a single tournament, added up across the
/// season (docs/vision.md).
/// <para>
/// Adding up the <b>points</b> is the whole of it. The official tiebreakers are not summed and not
/// recomputed across events: an opponents' match-win percentage describes one Swiss tournament, and
/// averaging somebody's rivals from March with their rivals from May does not describe anything.
/// What separates two players level on points is a product decision, written out in
/// <see cref="Rank"/> and in ADR-0037.
/// </para>
/// </summary>
public static class LeagueTable
{
    /// <summary>
    /// Ranks everyone who played. A <b>cancelled</b> tournament is skipped even if it is still
    /// pointing at the league: it scores nowhere, which is the same rule
    /// <see cref="Tournament.AssignToLeague"/> applies on the way in.
    /// </summary>
    public static IReadOnlyList<LeagueStanding> For(IEnumerable<Tournament> tournaments)
    {
        var season = new Dictionary<Guid, Tally>();

        // Oldest first, so the name each row ends up with is the most recent one that player used.
        foreach (var tournament in tournaments
            .Where(tournament => tournament.Status != TournamentStatus.Cancelled)
            .OrderBy(tournament => tournament.StartsAt))
        {
            var book = tournament.Participants.ToDictionary(
                participant => participant.Id,
                participant => participant.StorePlayerId);

            foreach (var standing in StandingsCalculator.For(tournament))
            {
                if (!book.TryGetValue(standing.ParticipantId, out var storePlayerId))
                {
                    continue;
                }

                if (!season.TryGetValue(storePlayerId, out var tally))
                {
                    tally = new Tally(storePlayerId);
                    season[storePlayerId] = tally;
                }

                tally.Add(standing);
            }
        }

        return Rank(season.Values.Where(tally => tally.Tournaments > 0));
    }

    /// <summary>
    /// Points first, and then two criteria that only ever separate players already level on them.
    /// <list type="bullet">
    /// <item><b>Match wins</b>, so nine wins beat eight wins and three draws worth the same.</item>
    /// <item><b>Fewer tournaments</b>, because the same points out of fewer events is the better
    /// season. It never costs anybody for turning up: it applies only at equal points.</item>
    /// <item>and the name, so the order of two players who are genuinely level does not change
    /// between two reads of the same page.</item>
    /// </list>
    /// </summary>
    private static List<LeagueStanding> Rank(IEnumerable<Tally> season) =>
    [
        .. season
            .OrderByDescending(tally => tally.Points)
            .ThenByDescending(tally => tally.Wins + tally.Byes)
            .ThenBy(tally => tally.Tournaments)
            .ThenBy(tally => tally.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select((tally, index) => tally.ToStanding(index + 1))
    ];

    private sealed class Tally(Guid storePlayerId)
    {
        public string Name { get; private set; } = string.Empty;

        public int Points { get; private set; }

        public int Tournaments { get; private set; }

        public int Wins { get; private set; }

        public int Losses { get; private set; }

        public int Draws { get; private set; }

        public int Byes { get; private set; }

        public void Add(Standing standing)
        {
            Name = standing.Name;
            Points += standing.MatchPoints;
            Wins += standing.Wins;
            Losses += standing.Losses;
            Draws += standing.Draws;
            Byes += standing.Byes;

            // Signing up for something that never got played is not a tournament played, and it
            // would otherwise count against them on the second tiebreaker.
            if (standing.RoundsPlayed > 0)
            {
                Tournaments++;
            }
        }

        public LeagueStanding ToStanding(int rank) =>
            new(storePlayerId, Name, rank, Points, Tournaments, Wins, Losses, Draws, Byes);
    }
}
