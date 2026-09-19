using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;

namespace TopCut.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes the domain events an aggregate raised, <b>after</b> the save succeeds, so a handler
/// never reacts to work that was rolled back. Events are drained from the tracked entities before
/// dispatch, so a handler that saves again cannot re-publish the same event.
/// </summary>
public sealed class DomainEventDispatchInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    private readonly List<DomainEvent> _pending = [];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Drain().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await Drain(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IHasDomainEvents>())
        {
            if (entry.Entity.DomainEvents.Count == 0)
            {
                continue;
            }

            _pending.AddRange(entry.Entity.DomainEvents);
            entry.Entity.ClearDomainEvents();
        }
    }

    private async Task Drain(CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = _pending.ToArray();
        _pending.Clear();

        await dispatcher.DispatchAsync(batch, cancellationToken);
    }
}
