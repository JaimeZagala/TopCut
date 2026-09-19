using TopCut.Domain.Common;

namespace TopCut.Application.Abstractions;

/// <summary>
/// Handles one kind of domain event. v1 ships no implementations: these exist so that turning on
/// push notifications or live-screen updates later is a matter of adding a handler (see ADR-0010).
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes the domain events an aggregate raised. Called after changes are committed, so a
/// handler never sees an event for work that was rolled back.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
