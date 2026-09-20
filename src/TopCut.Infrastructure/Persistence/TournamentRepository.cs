using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

public sealed class TournamentRepository(IDbContextFactory<TopCutDbContext> contexts) : ITournamentRepository
{
    public async Task<OperationResult> ExecuteAsync(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await LoadAsync(db, tournamentId, cancellationToken);

        if (tournament is null)
        {
            return OperationResult.Fail("No existe ese torneo.");
        }

        var result = work(tournament);

        // Only a successful operation is saved. A refused one throws its context away with whatever
        // half-applied state it had, so a rule broken midway cannot leave a trace behind.
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two people working the same tournament at once — the desk and a phone, say. The
            // RowVersion check caught it, which is the whole point of having one; what it must not
            // do is reach the organiser as a stack trace.
            return OperationResult.Fail(
                "Alguien ha cambiado este torneo mientras lo editabas. Recarga y vuelve a intentarlo.");
        }

        return result;
    }

    public async Task<T> ReadAsync<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await LoadAsync(db, tournamentId, cancellationToken, tracked: false);

        return tournament is null ? whenMissing : read(tournament);
    }

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Pulls the whole aggregate in one go. Pairing and standings both walk every round, so lazy
    /// loading would turn one operation into dozens of round trips against a database that, on the
    /// free tier, is charged by the second.
    /// <para>
    /// No <c>IgnoreQueryFilters</c> here: the tenant filter is exactly what stops a store from
    /// reaching another store's tournament by guessing its id.
    /// </para>
    /// </summary>
    private static Task<Tournament?> LoadAsync(
        TopCutDbContext db,
        Guid tournamentId,
        CancellationToken cancellationToken,
        bool tracked = true)
    {
        var tournaments = tracked ? db.Tournaments : db.Tournaments.AsNoTracking();

        return tournaments
            .Include(tournament => tournament.Participants)
            .Include(tournament => tournament.Rounds)
                .ThenInclude(round => round.Pairings)
                    .ThenInclude(pairing => pairing.Games)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tournament => tournament.Id == tournamentId, cancellationToken);
    }
}
