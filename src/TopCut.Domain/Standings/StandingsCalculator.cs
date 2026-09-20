using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Standings;

/// <summary>
/// Turns the reported results into a ranked table, applying the tournament's own tiebreakers in the
/// order its ruleset declares. No branch here asks which TCG it is looking at: the differences
/// between Magic and One Piece live entirely in the numbers the ruleset supplies.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>
    /// Ranks every participant, dropped players included. Results already reported in the round
    /// currently in progress count, which is what lets the live screen update as slips come in.
    /// </summary>
    public static IReadOnlyList<Standing> For(Tournament tournament)
    {
        var settings = tournament.Settings;
        var records = BuildRecords(tournament);

        // Every opponent percentage is an average of these, so they have to exist for everyone
        // before any of them can be averaged.
        var own = records.ToDictionary(
            record => record.Key,
            record => new OwnPercentages(
                MatchWin: Floored(record.Value.MatchPoints, record.Value.CountedRounds * settings.WinPoints, settings),
                GameWin: Floored(record.Value.GamePoints, record.Value.GamesPlayed * settings.GameWinPoints, settings)));

        var rows = records.Values
            .Select(record => new Standing(
                ParticipantId: record.ParticipantId,
                Name: record.Name,
                Rank: 0,
                MatchPoints: record.MatchPoints,
                Wins: record.Wins,
                Losses: record.Losses,
                Draws: record.Draws,
                Byes: record.Byes,
                OwnMatchWinPercentage: own[record.ParticipantId].MatchWin,
                OpponentsMatchWinPercentage: Average(record.Opponents, id => own[id].MatchWin, settings),
                GameWinPercentage: own[record.ParticipantId].GameWin,
                OpponentsGameWinPercentage: Average(record.Opponents, id => own[id].GameWin, settings),
                Dropped: record.Dropped))
            .ToList();

        return Rank(rows, settings, records);
    }

    private static Dictionary<Guid, Record> BuildRecords(Tournament tournament)
    {
        var records = tournament.Participants.ToDictionary(
            participant => participant.Id,
            participant => new Record(
                participant.Id,
                participant.Name,
                participant.Status == ParticipantStatus.Dropped));

        var settings = tournament.Settings;

        foreach (var round in tournament.Rounds)
        {
            foreach (var pairing in round.Pairings.Where(p => p.Status == PairingStatus.Reported))
            {
                Apply(records, pairing, round.Format, settings);
            }
        }

        return records;
    }

    private static void Apply(
        Dictionary<Guid, Record> records,
        Pairing pairing,
        MatchFormat format,
        TournamentSettings settings)
    {
        if (pairing.IsBye)
        {
            if (!records.TryGetValue(pairing.PlayerAId, out var onABye))
            {
                return;
            }

            onABye.Byes++;
            onABye.MatchPoints += settings.PointsFor(MatchResult.Bye);

            // A bye is scored as if it had been played and won cleanly: in Magic that is a 2-0, so
            // it also feeds the game percentages. One Piece keeps byes out of the player's own
            // numbers altogether, which is what this flag decides.
            if (settings.ByesCountInOwnWinPercentage)
            {
                onABye.CountedRounds++;
                onABye.GamesPlayed += format.GamesToWin();
                onABye.GamePoints += format.GamesToWin() * settings.GameWinPoints;
            }

            return;
        }

        var playerB = pairing.PlayerBId!.Value;

        if (!records.TryGetValue(pairing.PlayerAId, out var a) || !records.TryGetValue(playerB, out var b))
        {
            return;
        }

        Score(a, pairing.ResultFor(a.ParticipantId), settings);
        Score(b, pairing.ResultFor(b.ParticipantId), settings);

        a.CountedRounds++;
        b.CountedRounds++;
        a.Opponents.Add(b.ParticipantId);
        b.Opponents.Add(a.ParticipantId);

        var games = pairing.Games.Count;
        a.GamesPlayed += games;
        b.GamesPlayed += games;
        a.GamePoints += GamePointsFor(pairing.PlayerAWins, pairing.DrawnGames, settings);
        b.GamePoints += GamePointsFor(pairing.PlayerBWins, pairing.DrawnGames, settings);

        if (pairing.PlayerAWins > pairing.PlayerBWins)
        {
            a.BeatenOpponents.Add(b.ParticipantId);
        }
        else if (pairing.PlayerBWins > pairing.PlayerAWins)
        {
            b.BeatenOpponents.Add(a.ParticipantId);
        }
    }

    private static void Score(Record record, MatchResult result, TournamentSettings settings)
    {
        record.MatchPoints += settings.PointsFor(result);

        switch (result)
        {
            case MatchResult.Win:
                record.Wins++;
                break;
            case MatchResult.Loss:
                record.Losses++;
                break;
            case MatchResult.Draw:
                record.Draws++;
                break;
        }
    }

    private static int GamePointsFor(int won, int drawn, TournamentSettings settings) =>
        (won * settings.GameWinPoints) + (drawn * settings.GameDrawPoints);

    /// <summary>
    /// A percentage with the ruleset's floor applied. The floor exists so that one opponent's
    /// collapse does not drag down the tiebreakers of everyone who faced them.
    /// </summary>
    private static decimal Floored(int earned, int possible, TournamentSettings settings)
    {
        if (possible <= 0)
        {
            return settings.MinimumPercentage;
        }

        var value = (decimal)earned / possible;
        return value < settings.MinimumPercentage ? settings.MinimumPercentage : value;
    }

    private static decimal Average(
        List<Guid> opponents,
        Func<Guid, decimal> percentage,
        TournamentSettings settings) =>
        opponents.Count == 0
            ? settings.MinimumPercentage
            : opponents.Select(percentage).Average();

    private static List<Standing> Rank(
        List<Standing> rows,
        TournamentSettings settings,
        Dictionary<Guid, Record> records)
    {
        var ordered = rows
            .OrderByDescending(row => row.MatchPoints)
            .ThenByDescending(row => Numeric(row, settings, 0))
            .ThenByDescending(row => Numeric(row, settings, 1))
            .ThenByDescending(row => Numeric(row, settings, 2))
            .ThenByDescending(row => Numeric(row, settings, 3))
            .ToList();

        ordered = ApplyHeadToHead(ordered, settings, records);

        return [.. ordered.Select((row, index) => row with { Rank = index + 1 })];
    }

    /// <summary>
    /// The value of the Nth configured tiebreaker, or zero when this ruleset declares fewer.
    /// Head-to-head is not a number and is handled separately.
    /// </summary>
    private static decimal Numeric(Standing row, TournamentSettings settings, int position)
    {
        if (position >= settings.Tiebreakers.Count)
        {
            return 0m;
        }

        return settings.Tiebreakers[position] switch
        {
            Tiebreaker.OwnMatchWinPercentage => row.OwnMatchWinPercentage,
            Tiebreaker.OpponentsMatchWinPercentage => row.OpponentsMatchWinPercentage,
            Tiebreaker.GameWinPercentage => row.GameWinPercentage,
            Tiebreaker.OpponentsGameWinPercentage => row.OpponentsGameWinPercentage,
            _ => 0m
        };
    }

    /// <summary>
    /// Last resort, and only between two players who are still exactly level and who actually
    /// played each other: the winner of that match goes above.
    /// <para>
    /// Deliberately not applied to groups of three or more. Head-to-head can be circular — A beats
    /// B, B beats C, C beats A — so there is no consistent order to impose, and the rulebooks send
    /// those cases to a playoff or a die roll rather than to an algorithm.
    /// </para>
    /// </summary>
    private static List<Standing> ApplyHeadToHead(
        List<Standing> ordered,
        TournamentSettings settings,
        Dictionary<Guid, Record> records)
    {
        if (!settings.Tiebreakers.Contains(Tiebreaker.HeadToHead))
        {
            return ordered;
        }

        for (var i = 0; i + 1 < ordered.Count; i++)
        {
            var above = ordered[i];
            var below = ordered[i + 1];

            var stillLevel = above.MatchPoints == below.MatchPoints
                && Enumerable.Range(0, 4).All(position =>
                    Numeric(above, settings, position) == Numeric(below, settings, position));

            if (!stillLevel)
            {
                continue;
            }

            var isolated = (i == 0 || ordered[i - 1].MatchPoints != above.MatchPoints)
                && (i + 2 >= ordered.Count || ordered[i + 2].MatchPoints != above.MatchPoints);

            if (isolated && records[below.ParticipantId].BeatenOpponents.Contains(above.ParticipantId))
            {
                (ordered[i], ordered[i + 1]) = (below, above);
            }
        }

        return ordered;
    }

    private readonly record struct OwnPercentages(decimal MatchWin, decimal GameWin);

    private sealed class Record(Guid participantId, string name, bool dropped)
    {
        public Guid ParticipantId { get; } = participantId;

        public string Name { get; } = name;

        public bool Dropped { get; } = dropped;

        public int MatchPoints { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }

        public int Draws { get; set; }

        public int Byes { get; set; }

        /// <summary>Rounds that go into the denominator of this player's own percentage.</summary>
        public int CountedRounds { get; set; }

        public int GamePoints { get; set; }

        public int GamesPlayed { get; set; }

        public List<Guid> Opponents { get; } = [];

        public List<Guid> BeatenOpponents { get; } = [];
    }
}
