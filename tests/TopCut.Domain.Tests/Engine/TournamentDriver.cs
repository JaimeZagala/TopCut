using TopCut.Domain.Catalog;
using TopCut.Domain.Pairings;
using TopCut.Domain.Standings;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Domain.Tests.Engine;

/// <summary>
/// Runs whole tournaments in memory so the engine can be tested the way it will actually be used:
/// round after round, with real pairings feeding real standings feeding the next round's pairings.
/// <para>
/// Players are named P1, P2, ... in registration order, and by default the lower number wins every
/// match. That gives a completely predictable tournament, which is what makes it possible to assert
/// on exact pairings and exact tiebreaker values instead of on vague properties.
/// </para>
/// </summary>
internal sealed class TournamentDriver
{
    private static readonly DateTimeOffset Start = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<Guid, int> _seeds = [];
    private DateTimeOffset _now = Start;

    private TournamentDriver(Ruleset ruleset, int players, int swissRounds, int? topCut)
    {
        Tournament = Tournament.Create(
            Guid.CreateVersion7(),
            ruleset,
            "Torneo simulado",
            Start.AddDays(1),
            new Address("Calle Mayor 1", "Valencia", "Valencia", "46001"),
            capacity: Math.Max(players, 2),
            swissRounds: swissRounds);

        if (topCut is { } size)
        {
            Tournament.SetTopCut(size);
        }

        Tournament.Publish();

        for (var seed = 1; seed <= players; seed++)
        {
            var participant = Tournament.Register($"P{seed}");
            _seeds[participant.Id] = seed;
        }
    }

    public Tournament Tournament { get; }

    public static TournamentDriver Magic(int players, int swissRounds, int? topCut = null) =>
        new(SeedCatalog.MagicRuleset(tcgId: 1), players, swissRounds, topCut);

    /// <summary>Magic's scoring with a different tiebreaker order, to prove the order is data.</summary>
    public static TournamentDriver MagicWithTiebreakers(int players, int swissRounds, params Tiebreaker[] tiebreakers)
    {
        var ruleset = SeedCatalog.MagicRuleset(tcgId: 1);
        ruleset.SetTiebreakers(tiebreakers);
        return new TournamentDriver(ruleset, players, swissRounds, topCut: null);
    }

    public static TournamentDriver OnePiece(int players, int swissRounds, int? topCut = null) =>
        new(SeedCatalog.OnePieceRuleset(tcgId: 2), players, swissRounds, topCut);

    public int Seed(Guid participantId) => _seeds[participantId];

    public Guid Id(int seed) => _seeds.First(pair => pair.Value == seed).Key;

    public string Name(Guid participantId) => $"P{_seeds[participantId]}";

    public IReadOnlyList<Standing> Standings() => StandingsCalculator.For(Tournament);

    /// <summary>Ranked participant seeds, best first.</summary>
    public IReadOnlyList<int> RankedSeeds() => [.. Standings().OrderBy(s => s.Rank).Select(s => Seed(s.ParticipantId))];

    /// <summary>
    /// Opens the next round through the same entry point the application uses, so the tests
    /// exercise the real path and not a parallel one written for their convenience.
    /// </summary>
    public Round StartNextRound() => TournamentEngine.StartNextRound(Tournament, _now);

    public Round StartSwissRound() => StartNextRound();

    public Round StartTopCutRound() => StartNextRound();

    /// <summary>Reports every match of the round. By default the lower seed wins.</summary>
    public void ReportRound(Round round, Func<int, int, int>? winnerSeed = null)
    {
        winnerSeed ??= Math.Min;

        foreach (var pairing in round.Pairings.Where(p => !p.IsBye).ToList())
        {
            var a = Seed(pairing.PlayerAId);
            var b = Seed(pairing.PlayerBId!.Value);
            var winner = winnerSeed(a, b);

            Tournament.ReportResult(
                round.Id,
                pairing.Id,
                GamesFor(round.Format, winner == a),
                reportedBy: null);
        }
    }

    public void CloseRound(Round round)
    {
        _now = _now.AddHours(1);
        Tournament.CloseRound(round.Id, _now);
    }

    /// <summary>Plays the whole Swiss phase through, closing each round.</summary>
    public void PlaySwiss(Func<int, int, int>? winnerSeed = null)
    {
        for (var i = 0; i < Tournament.SwissRounds; i++)
        {
            var round = StartSwissRound();
            ReportRound(round, winnerSeed);
            CloseRound(round);
        }
    }

    /// <summary>Every pair of participants who have faced each other, as unordered seed pairs.</summary>
    public IEnumerable<(int, int)> PlayedMatchups() => Tournament.Rounds
        .SelectMany(round => round.Pairings)
        .Where(pairing => !pairing.IsBye)
        .Select(pairing =>
        {
            var a = Seed(pairing.PlayerAId);
            var b = Seed(pairing.PlayerBId!.Value);
            return a < b ? (a, b) : (b, a);
        });

    public IReadOnlyList<int> ByeRecipients() => [.. Tournament.Rounds
        .SelectMany(round => round.Pairings)
        .Where(pairing => pairing.IsBye)
        .Select(pairing => Seed(pairing.PlayerAId))];

    /// <summary>
    /// Opens a round with pairings chosen by the test rather than by the pairer. Standings tests
    /// need exact control of who played whom; letting the engine decide would make the expected
    /// tiebreaker values depend on the pairer, which is a separate thing being tested elsewhere.
    /// </summary>
    public Round StartRoundWith(params (int A, int B)[] pairs)
    {
        var proposed = pairs
            .Select((pair, index) => new ProposedPairing(index + 1, Id(pair.A), Id(pair.B)))
            .ToList();

        return Lay(Phase.Swiss, proposed);
    }

    /// <summary>Reports one match by seed. <paramref name="gamesLostByWinner"/> drives the game percentages.</summary>
    public void ReportMatch(Round round, int winnerSeed, int loserSeed, int gamesLostByWinner = 0)
    {
        var pairing = round.Pairings.Single(p =>
            !p.IsBye
            && new[] { Seed(p.PlayerAId), Seed(p.PlayerBId!.Value) }.Order().SequenceEqual(new[] { winnerSeed, loserSeed }.Order()));

        var winnerIsA = Seed(pairing.PlayerAId) == winnerSeed;
        var winnerSide = winnerIsA ? GameWinner.PlayerA : GameWinner.PlayerB;
        var loserSide = winnerIsA ? GameWinner.PlayerB : GameWinner.PlayerA;

        var games = new List<GameWinner>();

        // Losses first, then the winner closes it out: keeps the sequence legal, since a match
        // stops the moment someone reaches the games they need.
        for (var i = 0; i < gamesLostByWinner; i++)
        {
            games.Add(loserSide);
        }

        games.AddRange(Enumerable.Repeat(winnerSide, round.Format.GamesToWin()));

        Tournament.ReportResult(round.Id, pairing.Id, games, reportedBy: null);
    }

    private Round Lay(Phase phase, IReadOnlyList<ProposedPairing> proposed)
    {
        var round = Tournament.StartRound(phase, _now);

        foreach (var pairing in proposed)
        {
            round.Pair(pairing.Table, pairing.PlayerAId, pairing.PlayerBId);
        }

        return round;
    }

    /// <summary>A clean win in as few games as the format allows: 1-0 in Bo1, 2-0 in Bo3.</summary>
    private static GameWinner[] GamesFor(MatchFormat format, bool playerAWins)
    {
        var winner = playerAWins ? GameWinner.PlayerA : GameWinner.PlayerB;
        return [.. Enumerable.Repeat(winner, format.GamesToWin())];
    }
}
