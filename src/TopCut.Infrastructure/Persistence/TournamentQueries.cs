using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Read-only projections for the store's screens. These never load the aggregate: the counts come
/// back as counts, computed by SQL Server, instead of dragging every participant across the wire to
/// call <c>Count()</c> on them.
/// <para>
/// No <c>IgnoreQueryFilters</c>: the tenant filter is what makes "the current store's tournaments"
/// mean what it says.
/// </para>
/// </summary>
public sealed class TournamentQueries(IDbContextFactory<TopCutDbContext> contexts, TimeProvider clock)
    : ITournamentQueries
{
    public async Task<IReadOnlyList<TournamentListItem>> ForCurrentStoreAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var rows = await db.Tournaments
            .AsNoTracking()
            .Select(tournament => new
            {
                tournament.Id,
                tournament.Name,
                tournament.TcgId,
                tournament.StartsAt,
                tournament.Status,
                tournament.Capacity,
                tournament.SwissRounds,
                tournament.TopCutSize,
                City = tournament.Venue.City,
                Registered = tournament.Participants.Count(p => p.Status == ParticipantStatus.Registered),
                Waitlisted = tournament.Participants.Count(p => p.Status == ParticipantStatus.Waitlisted)
            })
            .ToListAsync(cancellationToken);

        var games = await GameNamesAsync(db, cancellationToken);

        // What the organiser wants at the top is the next event, not the furthest one away. Sorted
        // here rather than in SQL because a store's list is short and the rule reads better in C#.
        var now = clock.GetUtcNow();

        var ordered = rows
            .Where(row => row.StartsAt >= now)
            .OrderBy(row => row.StartsAt)
            .Concat(rows
                .Where(row => row.StartsAt < now)
                .OrderByDescending(row => row.StartsAt));

        return
        [
            .. ordered.Select(row => new TournamentListItem(
                row.Id,
                row.Name,
                NameOf(games, row.TcgId),
                CodeOf(games, row.TcgId),
                row.StartsAt,
                row.Status,
                row.Capacity,
                row.Registered,
                row.Waitlisted,
                row.SwissRounds,
                row.TopCutSize,
                row.City))
        ];
    }

    public async Task<TournamentDetails?> DetailsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var tournament = await db.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            return null;
        }

        var counts = await db.Participants
            .AsNoTracking()
            .Where(participant => participant.TournamentId == tournamentId)
            .GroupBy(participant => participant.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var games = await GameNamesAsync(db, cancellationToken);

        return new TournamentDetails(
            tournament.Id,
            tournament.Name,
            tournament.TcgId,
            NameOf(games, tournament.TcgId),
            tournament.StartsAt,
            tournament.Venue,
            tournament.Capacity,
            tournament.SwissRounds,
            tournament.TopCutSize,
            tournament.Status,
            tournament.SettingsLocked,
            tournament.LeagueId,
            Count(counts.Select(c => (c.Status, c.Count)), ParticipantStatus.Registered),
            Count(counts.Select(c => (c.Status, c.Count)), ParticipantStatus.Waitlisted),
            tournament.Settings.SwissPhase,
            tournament.Settings.TopCutPhase,
            tournament.Settings.TimeExpiry,
            tournament.Settings.RulesVersion);
    }

    public async Task<Roster> RosterAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        // The tenant filter is on Participant too, so another store's roster comes back empty
        // rather than visible.
        var everyone = await db.Participants
            .AsNoTracking()
            .Where(participant => participant.TournamentId == tournamentId)
            .Select(participant => new ParticipantRow(
                participant.Id,
                participant.Name,
                participant.Status,
                participant.WaitlistPosition,
                participant.RegisteredAt,
                participant.UserId != null))
            .ToListAsync(cancellationToken);

        return new Roster(
            // Alphabetical: at the counter the question is always "am I on the list?", and the
            // answer is quicker to find by name than by the order people happened to arrive in.
            [.. everyone.Where(row => row.Status == ParticipantStatus.Registered).OrderBy(row => row.Name)],
            // The queue, on the other hand, only makes sense in its own order.
            [.. everyone.Where(row => row.Status == ParticipantStatus.Waitlisted).OrderBy(row => row.WaitlistPosition)],
            [.. everyone.Where(row => row.Status == ParticipantStatus.Dropped).OrderBy(row => row.Name)]);
    }

    private static string NameOf(Dictionary<int, (string Code, string Name)> games, int tcgId) =>
        games.TryGetValue(tcgId, out var game) ? game.Name : string.Empty;

    private static string CodeOf(Dictionary<int, (string Code, string Name)> games, int tcgId) =>
        games.TryGetValue(tcgId, out var game) ? game.Code : string.Empty;

    private static int Count(IEnumerable<(ParticipantStatus Status, int Count)> counts, ParticipantStatus status) =>
        counts.FirstOrDefault(entry => entry.Status == status).Count;

    /// <summary>
    /// The catalog keyed by id. It is two rows in v1 and global to every store, so reading it whole
    /// is cheaper than joining it into each query.
    /// </summary>
    private static async Task<Dictionary<int, (string Code, string Name)>> GameNamesAsync(
        TopCutDbContext db,
        CancellationToken cancellationToken) =>
        await db.Tcgs
            .AsNoTracking()
            .ToDictionaryAsync(tcg => tcg.Id, tcg => (tcg.Code, tcg.Name), cancellationToken);
}
