using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>
/// Loads and saves the tournament aggregate whole. Every operation the organiser performs touches
/// participants, rounds, pairings and games together, so there is no useful way to load a slice.
/// <para>
/// Each call is <b>one unit of work on its own database connection</b> (ADR-0023). Handing out a
/// long-lived context instead would break as soon as two components of the same screen read at the
/// same time, which is the normal state of affairs in Blazor.
/// </para>
/// <para>
/// A tournament that belongs to another store is indistinguishable from one that does not exist:
/// the tenant filter removes it before the query returns, on purpose.
/// </para>
/// </summary>
public interface ITournamentRepository
{
    /// <summary>
    /// Loads the aggregate, hands it to <paramref name="work"/>, and saves <b>only</b> if that
    /// comes back successful. A refused operation leaves nothing behind, not even the part of the
    /// change that ran before the rule was broken.
    /// </summary>
    Task<OperationResult> ExecuteAsync(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads something off the aggregate without saving. Returns <paramref name="whenMissing"/>
    /// when the tournament does not exist or belongs to another store.
    /// </summary>
    Task<T> ReadAsync<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="ExecuteAsync"/>, but for a tournament belonging to a store the caller is
    /// <b>not</b> scoped to: a player signing up from the public sheet (ADR-0031).
    /// <para>
    /// The tenant filter is switched off, so the rule that replaces it is the same one the public
    /// search uses (ADR-0029): only a tournament that store has published, of a store that is
    /// approved and is not a test tenant. Anything else answers as if it did not exist.
    /// </para>
    /// </summary>
    Task<OperationResult> ExecuteOnPublicAsync(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The read counterpart of <see cref="ExecuteOnPublicAsync"/>: the whole aggregate for a
    /// tournament of a store the caller is not scoped to, under the same visibility rule.
    /// </summary>
    Task<T> ReadOnPublicAsync<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every tournament scoring towards one league, whole, in a single set of queries. The
    /// cumulative table adds up what each of them scored, so there is no slice of this to load
    /// (ADR-0037) — and loading them one by one would be a round trip per event.
    /// </summary>
    Task<T> ReadLeagueAsync<T>(
        Guid leagueId,
        Func<IReadOnlyList<Tournament>, T> read,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same for a league of a store the caller is not scoped to, under the visibility rule the
    /// rest of the public path uses (ADR-0029): a draft never reaches the season's table.
    /// </summary>
    Task<T> ReadPublicLeagueAsync<T>(
        Guid leagueId,
        Func<IReadOnlyList<Tournament>, T> read,
        CancellationToken cancellationToken = default);

    Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default);
}
