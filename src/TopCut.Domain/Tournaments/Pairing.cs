using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tournaments;

/// <summary>
/// A match in a round between two participants, or a bye when <see cref="PlayerBId"/> is null.
/// Carries optimistic concurrency because both players can report at the same time.
/// </summary>
public class Pairing : BusinessEntity, ITenantOwned, IHasRowVersion
{
    private readonly List<Game> _games = [];

    private Pairing() { }

    internal Pairing(Guid roundId, Guid tenantId, int table, Guid playerAId, Guid? playerBId)
    {
        DomainException.Require(table >= 1, "El número de mesa empieza en 1.");
        DomainException.Require(playerBId != playerAId, "Un participante no puede enfrentarse a sí mismo.");

        RoundId = roundId;
        TenantId = tenantId;
        Table = table;
        PlayerAId = playerAId;
        PlayerBId = playerBId;

        // A bye is never played: it is born already resolved.
        Status = IsBye ? PairingStatus.Reported : PairingStatus.AwaitingResult;
    }

    public Guid RoundId { get; private set; }

    public Guid TenantId { get; private set; }

    public int Table { get; private set; }

    public Guid PlayerAId { get; private set; }

    /// <summary>Null when the pairing is a bye.</summary>
    public Guid? PlayerBId { get; private set; }

    public PairingStatus Status { get; private set; }

    public IReadOnlyList<Game> Games => _games.AsReadOnly();

    /// <summary>The participant who reported, when a player reported rather than the store.</summary>
    public Guid? ReportedBy { get; private set; }

    /// <summary>The store amended the result, usually because the players disagreed.</summary>
    public bool AmendedByOrganizer { get; private set; }

    /// <summary>The clock decided this match, not the players.</summary>
    public bool DecidedByTimeExpiry { get; private set; }

    public uint RowVersion { get; set; }

    public bool IsBye => PlayerBId is null;

    public int PlayerAWins => _games.Count(g => g.Winner == GameWinner.PlayerA);

    public int PlayerBWins => _games.Count(g => g.Winner == GameWinner.PlayerB);

    public int DrawnGames => _games.Count(g => g.Winner == GameWinner.Draw);

    /// <summary>The result as seen from the given participant.</summary>
    public MatchResult ResultFor(Guid participantId)
    {
        DomainException.Require(
            participantId == PlayerAId || participantId == PlayerBId,
            "Ese participante no juega este emparejamiento.");
        DomainException.Require(
            Status == PairingStatus.Reported,
            "El emparejamiento aún no tiene resultado.");

        if (IsBye)
        {
            return MatchResult.Bye;
        }

        if (PlayerAWins == PlayerBWins)
        {
            return MatchResult.Draw;
        }

        var aWon = PlayerAWins > PlayerBWins;
        var isA = participantId == PlayerAId;

        return aWon == isA ? MatchResult.Win : MatchResult.Loss;
    }

    /// <summary>Games won by the given participant (for the game-based tiebreakers).</summary>
    public int GamesWonBy(Guid participantId)
    {
        DomainException.Require(
            participantId == PlayerAId || participantId == PlayerBId,
            "Ese participante no juega este emparejamiento.");

        return participantId == PlayerAId ? PlayerAWins : PlayerBWins;
    }

    /// <summary>
    /// Appends a single finished game as it happens. This is what makes
    /// <see cref="TimeExpiryPolicy.LeaderWins"/> meaningful: without per-game reporting there is no
    /// way to know who was ahead when the clock ran out. The match closes itself as soon as it is
    /// decided or the format runs out of games.
    /// </summary>
    internal void RecordGame(GameWinner winner, MatchFormat format)
    {
        DomainException.Require(!IsBye, "Un bye no admite resultado: ya está resuelto.");
        DomainException.Require(
            Status == PairingStatus.AwaitingResult,
            "El emparejamiento ya tiene resultado.");
        DomainException.Require(
            _games.Count < format.MaxGames(),
            $"Un enfrentamiento a {format} no admite más de {format.MaxGames()} partidas.");

        _games.Add(new Game(Id, TenantId, _games.Count + 1, winner));

        var needed = format.GamesToWin();
        var decided = PlayerAWins >= needed || PlayerBWins >= needed;

        if (decided || _games.Count == format.MaxGames())
        {
            Status = PairingStatus.Reported;
        }
    }

    /// <summary>A whole match reported at once, by a player or entered directly by the store.</summary>
    internal void Report(IReadOnlyList<GameWinner> games, MatchFormat format, Guid? reportedBy)
    {
        DomainException.Require(!IsBye, "Un bye no admite resultado: ya está resuelto.");
        Validate(games, format);

        _games.Clear();
        for (var i = 0; i < games.Count; i++)
        {
            _games.Add(new Game(Id, TenantId, i + 1, games[i]));
        }

        Status = PairingStatus.Reported;
        ReportedBy = reportedBy;
        DecidedByTimeExpiry = false;
    }

    /// <summary>An amendment by the store, typically after the players disagreed.</summary>
    internal void Amend(IReadOnlyList<GameWinner> games, MatchFormat format)
    {
        DomainException.Require(!IsBye, "Un bye no admite resultado: ya está resuelto.");
        Validate(games, format);

        _games.Clear();
        for (var i = 0; i < games.Count; i++)
        {
            _games.Add(new Game(Id, TenantId, i + 1, games[i]));
        }

        Status = PairingStatus.Reported;
        ReportedBy = null;
        DecidedByTimeExpiry = false;
        AmendedByOrganizer = true;
    }

    /// <summary>
    /// The clock ran out with no result reported. Under
    /// <see cref="TimeExpiryPolicy.LeaderWins"/> whatever games were already recorded stand, so a
    /// player 1-0 up takes the match; under <see cref="TimeExpiryPolicy.AlwaysDraw"/> the match is
    /// wiped to a draw. Either way the store can amend it afterwards.
    /// </summary>
    internal void ResolveOnTimeExpiry(TimeExpiryPolicy policy)
    {
        DomainException.Require(!IsBye, "Un bye no puede resolverse por tiempo.");
        DomainException.Require(
            Status == PairingStatus.AwaitingResult,
            "El emparejamiento ya tenía resultado.");

        if (policy == TimeExpiryPolicy.AlwaysDraw)
        {
            _games.Clear();
        }

        Status = PairingStatus.Reported;
        DecidedByTimeExpiry = true;
        ReportedBy = null;
    }

    private static void Validate(IReadOnlyList<GameWinner> games, MatchFormat format)
    {
        DomainException.Require(games.Count > 0, "Hay que reportar al menos una partida.");
        DomainException.Require(
            games.Count <= format.MaxGames(),
            $"Un enfrentamiento a {format} no admite más de {format.MaxGames()} partidas.");

        // A match stops the moment someone reaches the games they need, so any game reported after
        // that point means the report is wrong (a 2-1 with a third game that was never played).
        // Checking game by game keeps impossible results out.
        var needed = format.GamesToWin();
        var a = 0;
        var b = 0;

        foreach (var winner in games)
        {
            DomainException.Require(
                a < needed && b < needed,
                $"El enfrentamiento ya estaba decidido con {a}-{b}: sobran partidas.");

            if (winner == GameWinner.PlayerA)
            {
                a++;
            }
            else if (winner == GameWinner.PlayerB)
            {
                b++;
            }
        }
    }
}
