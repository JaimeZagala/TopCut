using TopCut.Domain.Catalog;
using TopCut.Domain.Common;

namespace TopCut.Domain.Events;

/// <summary>A round was started and its pairings are up.</summary>
public sealed record RoundStarted(Guid TenantId, Guid TournamentId, Guid RoundId, int Number, Phase Phase)
    : DomainEvent;

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

/// <summary>A seat opened up. <paramref name="PromotedParticipantId"/> is null if nobody was waiting.</summary>
public sealed record SeatFreed(Guid TenantId, Guid TournamentId, Guid? PromotedParticipantId) : DomainEvent;

/// <summary>The tournament was closed out.</summary>
public sealed record TournamentCompleted(Guid TenantId, Guid TournamentId) : DomainEvent;
