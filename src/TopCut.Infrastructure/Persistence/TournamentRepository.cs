using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

public sealed class TournamentRepository(TopCutDbContext db) : ITournamentRepository
{
    /// <summary>
    /// Pulls the whole aggregate in one go. Pairing and standings both walk every round, so lazy
    /// loading would turn one operation into dozens of round trips against a database that, on the
    /// free tier, is charged by the second.
    /// <para>
    /// No <c>IgnoreQueryFilters</c> here: the tenant filter is exactly what stops a store from
    /// reaching another store's tournament by guessing its id.
    /// </para>
    /// </summary>
    public Task<Tournament?> FindAsync(Guid tournamentId, CancellationToken cancellationToken = default) =>
        db.Tournaments
            .Include(tournament => tournament.Participants)
            .Include(tournament => tournament.Rounds)
                .ThenInclude(round => round.Pairings)
                    .ThenInclude(pairing => pairing.Games)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tournament => tournament.Id == tournamentId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
