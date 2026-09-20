namespace TopCut.Application.Abstractions;

/// <summary>What happened, so a screen can decide whether it cares.</summary>
public enum TournamentChange
{
    /// <summary>A round opened and its pairings are up.</summary>
    RoundStarted,

    /// <summary>A round was closed, so the room screen moves on to the standings.</summary>
    RoundClosed,

    /// <summary>A result came in or was corrected.</summary>
    ResultRecorded,

    /// <summary>The clock ran out and the unreported matches were settled.</summary>
    TimeExpired,

    /// <summary>Somebody dropped and a seat may have changed hands.</summary>
    RosterChanged,

    /// <summary>The tournament was closed out.</summary>
    Completed
}

/// <summary>
/// Announces that a tournament moved. Published from a domain event handler, after the change is
/// committed (ADR-0010), so nobody is ever told about work that was rolled back.
/// </summary>
public interface ITournamentBroadcaster
{
    Task PublishAsync(Guid tournamentId, TournamentChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// The other side: a screen saying "tell me when this tournament moves".
/// <para>
/// Carries no data on purpose. The listener re-reads the tournament through the query it already
/// uses, so there is only ever one description of what a screen shows, and a viewer who reconnects
/// after missing a message still ends up on the current state rather than on a patched-up guess.
/// </para>
/// </summary>
public interface ITournamentWatch
{
    /// <summary>Listens until the returned handle is disposed.</summary>
    IDisposable Watch(Guid tournamentId, Func<TournamentChange, Task> onChange);
}
