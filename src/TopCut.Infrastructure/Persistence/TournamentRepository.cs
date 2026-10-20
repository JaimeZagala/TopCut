using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

public sealed class TournamentRepository(IDbContextFactory<TopCutDbContext> contexts) : ITournamentRepository
{
    public Task<OperationResult> ExecuteAsync(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        CancellationToken cancellationToken = default) =>
        Execute(tournamentId, work, onlyPublic: false, cancellationToken);

    public Task<OperationResult> ExecuteOnPublicAsync(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        CancellationToken cancellationToken = default) =>
        Execute(tournamentId, work, onlyPublic: true, cancellationToken);

    private async Task<OperationResult> Execute(
        Guid tournamentId,
        Func<Tournament, OperationResult> work,
        bool onlyPublic,
        CancellationToken cancellationToken)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await LoadAsync(db, tournamentId, cancellationToken, onlyPublic: onlyPublic);

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

        GuardTheWholeAggregate(db, tournament);

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

    /// <summary>
    /// Makes the tournament's own row part of every write to its aggregate, so its
    /// <c>RowVersion</c> is checked even when the change was to a child row.
    /// <para>
    /// Without this, two players pressing "Apuntarme" at the same time for the last seat both
    /// insert a <c>Participant</c> and <b>neither write collides</b>: the tournament row never
    /// changed, so there was no version for the database to compare. The tournament would quietly
    /// end up with one player more than it has seats.
    /// </para>
    /// <para>
    /// The aggregate root's version guards the whole aggregate, which is what a root is for.
    /// </para>
    /// </summary>
    private static void GuardTheWholeAggregate(TopCutDbContext db, Tournament tournament) =>
        db.Entry(tournament).Property(candidate => candidate.UpdatedAt).IsModified = true;

    public Task<T> ReadAsync<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        CancellationToken cancellationToken = default) =>
        Read(tournamentId, read, whenMissing, onlyPublic: false, cancellationToken);

    public Task<T> ReadOnPublicAsync<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        CancellationToken cancellationToken = default) =>
        Read(tournamentId, read, whenMissing, onlyPublic: true, cancellationToken);

    private async Task<T> Read<T>(
        Guid tournamentId,
        Func<Tournament, T> read,
        T whenMissing,
        bool onlyPublic,
        CancellationToken cancellationToken)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await LoadAsync(db, tournamentId, cancellationToken, tracked: false, onlyPublic);

        return tournament is null ? whenMissing : read(tournament);
    }

    public Task<T> ReadLeagueAsync<T>(
        Guid leagueId,
        Func<IReadOnlyList<Tournament>, T> read,
        CancellationToken cancellationToken = default) =>
        ReadLeague(leagueId, read, onlyPublic: false, cancellationToken);

    public Task<T> ReadPublicLeagueAsync<T>(
        Guid leagueId,
        Func<IReadOnlyList<Tournament>, T> read,
        CancellationToken cancellationToken = default) =>
        ReadLeague(leagueId, read, onlyPublic: true, cancellationToken);

    private async Task<T> ReadLeague<T>(
        Guid leagueId,
        Func<IReadOnlyList<Tournament>, T> read,
        bool onlyPublic,
        CancellationToken cancellationToken)
    {
        await using var db = contexts.CreateDbContext();

        var tournaments = await Aggregates(db, tracked: false, onlyPublic)
            .Where(tournament => tournament.LeagueId == leagueId)
            .ToListAsync(cancellationToken);

        return read(tournaments);
    }

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<Tournament?> LoadAsync(
        TopCutDbContext db,
        Guid tournamentId,
        CancellationToken cancellationToken,
        bool tracked = true,
        bool onlyPublic = false) =>
        Aggregates(db, tracked, onlyPublic)
            .SingleOrDefaultAsync(tournament => tournament.Id == tournamentId, cancellationToken);

    /// <summary>
    /// Pulls whole aggregates in one go. Pairing and standings both walk every round, so lazy
    /// loading would turn one operation into dozens of round trips against a database that, on the
    /// free tier, is charged by the second.
    /// <para>
    /// <b>Ordered on purpose.</b> Without it the database returns the rows in whatever order suits it,
    /// which is not the order they were created in: the round buttons came out as 2, 4, 3, 1.
    /// </para>
    /// <para>
    /// No <c>IgnoreQueryFilters</c> unless <paramref name="onlyPublic"/> asks for it: the tenant
    /// filter is exactly what stops a store from reaching another store's tournament by guessing
    /// its id.
    /// </para>
    /// </summary>
    private static IQueryable<Tournament> Aggregates(TopCutDbContext db, bool tracked, bool onlyPublic)
    {
        var tournaments = tracked ? db.Tournaments : db.Tournaments.AsNoTracking();

        // Reaching into another store's tournament, which only the public path does: the tenant
        // filter comes off and the public visibility rule goes in its place (ADR-0031).
        if (onlyPublic)
        {
            tournaments = tournaments.IgnoreQueryFilters().OnlyPublic(db);
        }

        return tournaments
            // Ordered like the rounds below, and for the same reason. Without it the pairer sees
            // the participants in whatever order the database felt like returning, so the same
            // tournament could pair differently from one load to the next.
            .Include(tournament => tournament.Participants.OrderBy(p => p.RegisteredAt).ThenBy(p => p.Id))
            .Include(tournament => tournament.Rounds.OrderBy(round => round.Number))
                .ThenInclude(round => round.Pairings.OrderBy(pairing => pairing.Table))
                    .ThenInclude(pairing => pairing.Games.OrderBy(game => game.Number))
            .AsSplitQuery();
    }
}
