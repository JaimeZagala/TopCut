using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>Where a player stands in one tournament. <c>null</c> from the query means "not signed up".</summary>
public sealed record PlayerEntry(
    Guid ParticipantId,
    string Name,
    ParticipantStatus Status,
    int? WaitlistPosition)
{
    public bool HoldsASeat => Status == ParticipantStatus.Registered;

    public bool IsWaiting => Status == ParticipantStatus.Waitlisted;

    /// <summary>They signed up at some point and then pulled out. Not the same as never having been in.</summary>
    public bool HasWithdrawn => Status == ParticipantStatus.Dropped;
}

/// <summary>One line of "Mis torneos": the tournament, plus where this player stands in it.</summary>
public sealed record PlayerHistoryRow(
    Guid TournamentId,
    string Name,
    string StoreName,
    string TcgName,
    DateTimeOffset StartsAt,
    string City,
    TournamentStatus TournamentStatus,
    ParticipantStatus Status,
    int? WaitlistPosition);

/// <summary>
/// A player's own sign-ups, read across every store they have played at. Like
/// <see cref="IPublicTournaments"/> this crosses the tenant boundary on purpose (ADR-0029), but
/// the rule that replaces the filter is a different one: not "what is public" but "what is
/// <b>this account's</b>", which is narrower and keyed on the user id.
/// </summary>
public interface IPlayerEntries
{
    Task<PlayerEntry?> ForAsync(Guid tournamentId, string userId, CancellationToken cancellationToken = default);

    /// <param name="past">Tournaments already played instead of the ones still to come.</param>
    Task<IReadOnlyList<PlayerHistoryRow>> HistoryAsync(
        string userId,
        bool past = false,
        CancellationToken cancellationToken = default);
}
