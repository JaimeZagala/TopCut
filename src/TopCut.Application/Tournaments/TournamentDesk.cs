using TopCut.Domain.Catalog;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Application.Tournaments;

/// <summary>One match as the organiser's desk shows it, with the names already resolved.</summary>
/// <param name="DecidedByTimeExpiry">Settled by the clock rather than reported, so worth flagging.</param>
/// <param name="AmendedByOrganizer">The store overrode what was reported, usually after a dispute.</param>
public sealed record DeskMatch(
    Guid PairingId,
    int Table,
    Guid PlayerAId,
    string PlayerAName,
    Guid? PlayerBId,
    string? PlayerBName,
    PairingStatus Status,
    int PlayerAWins,
    int PlayerBWins,
    bool DecidedByTimeExpiry,
    bool AmendedByOrganizer)
{
    public bool IsBye => PlayerBId is null;

    public bool Pending => Status == PairingStatus.AwaitingResult;

    /// <summary>The score from player A's side, which is how the desk reads the table.</summary>
    public string Score => $"{PlayerAWins}-{PlayerBWins}";
}

/// <summary>The round currently on screen.</summary>
public sealed record DeskRound(
    Guid RoundId,
    int Number,
    Phase Phase,
    RoundStatus Status,
    MatchFormat Format,
    DateTimeOffset? RegularTimeEndsAt,
    DateTimeOffset? ExtraTimeEndsAt,
    bool ExtraTimeExpired,
    IReadOnlyList<DeskMatch> Matches)
{
    public int Pending => Matches.Count(match => match.Pending);

    public bool AllReported => Pending == 0;

    public bool IsOpen => Status == RoundStatus.InProgress;
}

/// <summary>An entry in the round switcher.</summary>
public sealed record DeskRoundLink(int Number, Phase Phase, RoundStatus Status);

/// <summary>
/// Everything the control desk needs, built from <b>one</b> read of the aggregate.
/// <para>
/// Deliberately not assembled from several queries: the pairings and the standings are two views of
/// the same state, and pulling them separately could show an organiser a table that disagrees with
/// the matches right above it.
/// </para>
/// </summary>
/// <param name="CanStartNextRound">There is a round to launch and none open.</param>
/// <param name="CanApplyTimeExpiry">The clock is spent and something is still unreported.</param>
/// <param name="CanComplete">Every round is closed and there is nothing left to play.</param>
public sealed record TournamentDesk(
    Guid TournamentId,
    string Name,
    TournamentStatus Status,
    int SwissRounds,
    int? TopCutSize,
    int ActivePlayers,
    IReadOnlyList<DeskRoundLink> Rounds,
    DeskRound? Round,
    IReadOnlyList<Standing> Standings,
    IReadOnlyList<Tiebreaker> Tiebreakers,
    bool CanStartNextRound,
    Phase? NextPhase,
    int NextRoundNumber,
    bool CanApplyTimeExpiry,
    bool CanComplete,
    bool CanCloseRound);
