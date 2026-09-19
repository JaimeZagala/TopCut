namespace TopCut.Domain.Common;

/// <summary>
/// A business event published when changes are saved. Nothing subscribes to these in v1: they
/// exist so that adding notifications (Firebase on Android, Web Push in the browser) or refreshing
/// the live screen over SignalR is a matter of writing a handler, not touching domain logic.
/// </summary>
public abstract record DomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
