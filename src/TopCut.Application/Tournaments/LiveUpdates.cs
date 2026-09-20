using TopCut.Application.Abstractions;
using TopCut.Domain.Events;

namespace TopCut.Application.Tournaments;

/// <summary>
/// The first subscribers to the domain events (ADR-0010). Until now nothing listened: the events
/// were raised and dropped, which was the point — the live screens are wired on here without the
/// tournament rules learning that screens exist.
/// <para>
/// Each one does the same tiny thing, so they are grouped in one file rather than five.
/// </para>
/// </summary>
public static class LiveUpdates
{
    public sealed class OnRoundStarted(ITournamentBroadcaster broadcaster) : IDomainEventHandler<RoundStarted>
    {
        public Task HandleAsync(RoundStarted domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.RoundStarted, cancellationToken);
    }

    public sealed class OnRoundClosed(ITournamentBroadcaster broadcaster) : IDomainEventHandler<RoundClosed>
    {
        public Task HandleAsync(RoundClosed domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.RoundClosed, cancellationToken);
    }

    public sealed class OnResultRecorded(ITournamentBroadcaster broadcaster) : IDomainEventHandler<ResultRecorded>
    {
        public Task HandleAsync(ResultRecorded domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.ResultRecorded, cancellationToken);
    }

    public sealed class OnRoundTimeExpired(ITournamentBroadcaster broadcaster) : IDomainEventHandler<RoundTimeExpired>
    {
        public Task HandleAsync(RoundTimeExpired domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.TimeExpired, cancellationToken);
    }

    public sealed class OnSeatFreed(ITournamentBroadcaster broadcaster) : IDomainEventHandler<SeatFreed>
    {
        public Task HandleAsync(SeatFreed domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.RosterChanged, cancellationToken);
    }

    public sealed class OnTournamentCompleted(ITournamentBroadcaster broadcaster)
        : IDomainEventHandler<TournamentCompleted>
    {
        public Task HandleAsync(TournamentCompleted domainEvent, CancellationToken cancellationToken = default) =>
            broadcaster.PublishAsync(domainEvent.TournamentId, TournamentChange.Completed, cancellationToken);
    }
}
