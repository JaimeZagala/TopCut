using TopCut.Domain.Catalog;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <summary>
/// The player's own match in the round under way, read <b>from their side</b>: their opponent,
/// their score. The desk reads every table from player A's side because it is looking at a list;
/// somebody sitting at one table only ever thinks in terms of "I" and "them".
/// </summary>
/// <param name="IAmPlayerA">
/// Which side of the pairing they are. Results are stored from A's side, so this is what a report
/// coming off their screen has to be turned around by.
/// </param>
public sealed record PlayerMatch(
    Guid RoundId,
    Guid PairingId,
    int Table,
    string OpponentName,
    bool IsBye,
    bool Reported,
    int MyGames,
    int OpponentGames,
    bool ReportedByMe,
    bool DecidedByTimeExpiry,
    bool AmendedByOrganizer,
    MatchFormat Format,
    bool IAmPlayerA)
{
    /// <summary>Waiting on a result, and it is theirs to give.</summary>
    public bool CanReport => !IsBye && !Reported;

    /// <summary>"2-1" from their side, once there is something to show.</summary>
    public string Score => $"{MyGames}-{OpponentGames}";

    public bool IWon => Reported && (IsBye || MyGames > OpponentGames);

    public bool ItWasADraw => Reported && !IsBye && MyGames == OpponentGames;
}

/// <summary>
/// What one player sees of a tournament they are in: where they sit, what the clock says, and where
/// they stand. Built from a single read of the aggregate, like the desk, so the match and the
/// standing cannot contradict each other.
/// </summary>
/// <param name="ServerNow">
/// The server's clock when this was read, so the countdown can correct for the phone's own being
/// off (ADR-0028).
/// </param>
public sealed record PlayerBoard(
    Guid TournamentId,
    string Name,
    TournamentStatus Status,
    Guid ParticipantId,
    string PlayerName,
    ParticipantStatus PlayerStatus,
    int? RoundNumber,
    Phase? Phase,
    RoundStatus? RoundStatus,
    DateTimeOffset? RegularTimeEndsAt,
    DateTimeOffset? ExtraTimeEndsAt,
    PlayerMatch? Match,
    int? Rank,
    int MatchPoints,
    string Record,
    DateTimeOffset ServerNow)
{
    public bool UnderWay => Status == TournamentStatus.InProgress;

    /// <summary>They are in the tournament but this round has no table for them — or none is open.</summary>
    public bool Waiting => Match is null;

    /// <summary>A round with a clock is running. Both instants are set together or not at all.</summary>
    public bool ClockRunning =>
        RoundStatus == Domain.Tournaments.RoundStatus.InProgress && RegularTimeEndsAt is not null;
}
