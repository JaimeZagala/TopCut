using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Events;

/// <summary>A round was started and its pairings are up.</summary>
public sealed record RoundStarted(Guid TenantId, Guid TournamentId, Guid RoundId, int Number, Phase Phase)
    : DomainEvent;

/// <summary>
/// A round was closed. The pair of <see cref="RoundStarted"/>: without it the screen in the room
/// would keep showing a finished round, clock and all, until somebody reloaded it.
/// </summary>
public sealed record RoundClosed(Guid TenantId, Guid TournamentId, Guid RoundId, int Number) : DomainEvent;

/// <summary>A match result was recorded or amended.</summary>
public sealed record ResultRecorded(Guid TenantId, Guid TournamentId, Guid RoundId, Guid PairingId)
    : DomainEvent;

/// <summary>
/// The round clock ran out with matches still unreported. Those matches are already resolved
/// according to the tournament's time expiry policy; the ids are here so the organiser can be
/// pointed straight at the ones worth double-checking.
/// </summary>
public sealed record RoundTimeExpired(
    Guid TenantId,
    Guid TournamentId,
    Guid RoundId,
    IReadOnlyList<Guid> AffectedPairingIds) : DomainEvent;

/// <summary>
/// Somebody joined the tournament, whether the store typed them in at the counter or they signed
/// up themselves from the public sheet. <paramref name="Waitlisted"/> tells the two apart for a
/// screen that cares: taking the last seat and joining the queue are not the same news.
/// </summary>
public sealed record ParticipantRegistered(
    Guid TenantId,
    Guid TournamentId,
    Guid ParticipantId,
    bool Waitlisted) : DomainEvent;

/// <summary>
/// Somebody left: a withdrawal at the counter, or a player unsigning from the public sheet. It is
/// raised for anyone leaving, seat holder or queue, because either way the roster on screen is now
/// out of date.
/// </summary>
/// <param name="PromotedParticipantId">
/// Whoever moved up into the freed seat, or null when nobody did — because nobody was waiting, or
/// because the person leaving was in the queue themselves.
/// </param>
public sealed record ParticipantDropped(
    Guid TenantId,
    Guid TournamentId,
    Guid ParticipantId,
    Guid? PromotedParticipantId) : DomainEvent;

/// <summary>The tournament was closed out.</summary>
public sealed record TournamentCompleted(Guid TenantId, Guid TournamentId) : DomainEvent;
