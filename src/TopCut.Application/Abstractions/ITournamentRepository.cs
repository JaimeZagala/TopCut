using TopCut.Domain.Tournaments;

namespace TopCut.Application.Abstractions;

/// <summary>
/// Loads and saves the tournament aggregate whole. Every operation the organiser performs touches
/// participants, rounds, pairings and games together, so there is no useful way to load a slice.
/// </summary>
public interface ITournamentRepository
{
    /// <summary>
    /// The tournament with its participants, rounds, pairings and individual games loaded. Returns
    /// <c>null</c> when it does not exist <b>or</b> belongs to another store: the tenant filter
    /// makes those two cases indistinguishable on purpose.
    /// </summary>
    Task<Tournament?> FindAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
