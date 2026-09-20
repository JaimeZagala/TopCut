using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Live;

/// <summary>
/// In-memory fan-out from a committed change to whoever is watching that tournament (ADR-0027).
/// <para>
/// There is no second SignalR hub behind this. Every screen in the web application is a Blazor
/// Server page, which already holds a SignalR circuit; pushing through <i>that</i> costs nothing
/// extra, while a hub of its own would mean a second WebSocket per viewer — and the free App
/// Service tier allows five in total.
/// </para>
/// <para>
/// Singleton, because the publisher and the listeners live in different scopes: the publisher is a
/// domain event handler inside whoever saved the change, and the listeners are other people's
/// circuits.
/// </para>
/// </summary>
public sealed class TournamentBroadcaster(ILogger<TournamentBroadcaster> logger)
    : ITournamentBroadcaster, ITournamentWatch
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Func<TournamentChange, Task>>> _watchers = new();

    public IDisposable Watch(Guid tournamentId, Func<TournamentChange, Task> onChange)
    {
        var id = Guid.CreateVersion7();

        _watchers.GetOrAdd(tournamentId, _ => new ConcurrentDictionary<Guid, Func<TournamentChange, Task>>())[id] = onChange;

        return new Subscription(() =>
        {
            if (_watchers.TryGetValue(tournamentId, out var listeners))
            {
                listeners.TryRemove(id, out _);

                // Nobody left watching this tournament: stop holding a bucket for it.
                if (listeners.IsEmpty)
                {
                    _watchers.TryRemove(tournamentId, out _);
                }
            }
        });
    }

    public async Task PublishAsync(
        Guid tournamentId,
        TournamentChange change,
        CancellationToken cancellationToken = default)
    {
        if (!_watchers.TryGetValue(tournamentId, out var listeners))
        {
            return;
        }

        foreach (var listener in listeners.Values)
        {
            try
            {
                await listener(change);
            }
            catch (Exception error)
            {
                // One screen that has gone away — a closed tab, a dropped circuit — must not stop
                // the others from being told, and must certainly not fail the save that got here.
                logger.LogWarning(error, "No se pudo avisar a una pantalla del torneo {TournamentId}.", tournamentId);
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            dispose();
        }
    }
}
