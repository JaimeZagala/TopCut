using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// A player's own sign-ups. Crosses the tenant boundary (ADR-0029) because a player's history is
/// theirs and not any one store's: they may have played at three shops in two cities.
/// <para>
/// What replaces the filter here is the user id, which comes from the authenticated principal and
/// never from the address bar. Every query in this class is keyed on it.
/// </para>
/// </summary>
public sealed class PlayerEntries(IDbContextFactory<TopCutDbContext> contexts, TimeProvider clock) : IPlayerEntries
{
    public async Task<PlayerEntry?> ForAsync(
        Guid tournamentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        return await db.Participants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(participant => participant.TournamentId == tournamentId && participant.UserId == userId)
            .Select(participant => new PlayerEntry(
                participant.Id,
                participant.Name,
                participant.Status,
                participant.WaitlistPosition))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerHistoryRow>> HistoryAsync(
        string userId,
        bool past = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        // A withdrawn entry is left out: "mis torneos" is where the player is going, not a record
        // of everything they ever changed their mind about.
        var query = db.Participants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(participant =>
                participant.UserId == userId
                && participant.Status != ParticipantStatus.Dropped);

        var now = clock.GetUtcNow();

        var rows = await query
            .Select(participant => new
            {
                participant.Status,
                participant.WaitlistPosition,
                Tournament = db.Tournaments
                    .IgnoreQueryFilters()
                    .Where(tournament => tournament.Id == participant.TournamentId)
                    .Select(tournament => new
                    {
                        tournament.Id,
                        tournament.Name,
                        tournament.TcgId,
                        tournament.StartsAt,
                        tournament.Status,
                        City = tournament.Venue.City,
                        StoreName = db.Stores
                            .Where(store => store.Id == tournament.TenantId)
                            .Select(store => store.Name)
                            .First()
                    })
                    .First()
            })
            .ToListAsync(cancellationToken);

        var games = await db.Tcgs
            .AsNoTracking()
            .ToDictionaryAsync(tcg => tcg.Id, tcg => tcg.Name, cancellationToken);

        var matching = past
            ? rows.Where(row => row.Tournament.StartsAt < now).OrderByDescending(row => row.Tournament.StartsAt)
            : rows.Where(row => row.Tournament.StartsAt >= now).OrderBy(row => row.Tournament.StartsAt);

        return
        [
            .. matching.Select(row => new PlayerHistoryRow(
                row.Tournament.Id,
                row.Tournament.Name,
                row.Tournament.StoreName,
                games.GetValueOrDefault(row.Tournament.TcgId, string.Empty),
                row.Tournament.StartsAt,
                row.Tournament.City,
                row.Tournament.Status,
                row.Status,
                row.WaitlistPosition))
        ];
    }
}
