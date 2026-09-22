using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Standings;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The public page of a league (ADR-0037). Another of the deliberate crossings of the tenant
/// boundary (ADR-0029), and it starts from the same rule as the rest of them.
/// <para>
/// A league is public <b>when at least one of its tournaments is</b>. Asking it that way means
/// there is no second notion of "published" to keep in step with the first, and a season the store
/// is still putting together in drafts simply has no address.
/// </para>
/// </summary>
public sealed class PublicLeagues(
    IDbContextFactory<TopCutDbContext> contexts,
    ITournamentRepository tournaments) : IPublicLeagues
{
    public async Task<PublicLeagueInfo?> FindAsync(
        Guid leagueId,
        string? viewerUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var events = await Visible(db, leagueId)
            .OrderBy(tournament => tournament.StartsAt)
            .Select(tournament => new PublicLeagueTournament(
                tournament.Id,
                tournament.Name,
                tournament.StartsAt,
                tournament.Status,
                tournament.Participants.Count(p => p.Status != ParticipantStatus.Dropped)))
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return null;
        }

        var league = await db.Leagues
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == leagueId, cancellationToken);

        if (league is null)
        {
            return null;
        }

        var storeName = await db.Stores
            .AsNoTracking()
            .Where(store => store.Id == league.TenantId)
            .Select(store => store.Name)
            .SingleAsync(cancellationToken);

        var tcgName = await db.Tcgs
            .AsNoTracking()
            .Where(tcg => tcg.Id == league.TcgId)
            .Select(tcg => tcg.Name)
            .SingleAsync(cancellationToken);

        // The same table the store sees, minus whatever it has not published: a draft's roster is
        // the store's own workspace and has no business showing up in a public standing.
        var standings = await tournaments.ReadPublicLeagueAsync(leagueId, LeagueTable.For, cancellationToken);

        return new PublicLeagueInfo(
            league.Id,
            league.Name,
            storeName,
            tcgName,
            league.StartsOn,
            league.EndsOn,
            league.IsOpen,
            standings,
            events,
            await ViewerAsync(db, viewerUserId, league.TenantId, cancellationToken));
    }

    public async Task<bool> IsPublicAsync(Guid leagueId, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        return await Visible(db, leagueId).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerLeagueRow>> ForPlayerAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var book = await BookOfAsync(db, userId, cancellationToken);

        if (book.Count == 0)
        {
            return [];
        }

        var ids = await db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OnlyPublic(db)
            .Where(tournament => tournament.LeagueId != null
                && tournament.Participants.Any(participant => book.Contains(participant.StorePlayerId)))
            .Select(tournament => tournament.LeagueId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return [];
        }

        var leagues = await db.Leagues
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(league => ids.Contains(league.Id))
            .Select(league => new
            {
                league.Id,
                league.Name,
                league.TenantId,
                league.TcgId,
                league.StartsOn,
                league.EndsOn,
                league.IsOpen
            })
            .ToListAsync(cancellationToken);

        var stores = await db.Stores
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(store => leagues.Select(league => league.TenantId).Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        var games = await db.Tcgs
            .AsNoTracking()
            .ToDictionaryAsync(tcg => tcg.Id, tcg => tcg.Name, cancellationToken);

        var rows = new List<PlayerLeagueRow>();

        // One table per league, and a table is the aggregates of its tournaments (ADR-0037). A
        // player is in one or two leagues, so this stays small; it is also why this page is its own
        // address instead of a section of "Mis torneos", which is opened far more often.
        foreach (var league in leagues)
        {
            var table = await tournaments.ReadPublicLeagueAsync(league.Id, LeagueTable.For, cancellationToken);
            var mine = table.FirstOrDefault(row => book.Contains(row.StorePlayerId));

            if (mine is null)
            {
                // Signed up for one of its tournaments, never played a round in it yet.
                continue;
            }

            rows.Add(new PlayerLeagueRow(
                league.Id,
                league.Name,
                stores.GetValueOrDefault(league.TenantId, string.Empty),
                games.GetValueOrDefault(league.TcgId, string.Empty),
                league.StartsOn,
                league.EndsOn,
                league.IsOpen,
                mine.Rank,
                table.Count,
                mine.Points,
                mine.Tournaments));
        }

        // The season being played now is the one they came to look at.
        return [.. rows.OrderByDescending(row => row.IsOpen).ThenByDescending(row => row.StartsOn)];
    }

    /// <summary>
    /// This account's live book entries. A folded one let go of the account when it was merged
    /// (ADR-0035), so these are the entries that own the results.
    /// </summary>
    private static async Task<List<Guid>> BookOfAsync(
        TopCutDbContext db,
        string userId,
        CancellationToken cancellationToken) =>
        await db.StorePlayers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(player => player.UserId == userId && player.MergedIntoId == null)
            .Select(player => player.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Which row of this league's table belongs to whoever is reading it, if any.</summary>
    private static async Task<Guid?> ViewerAsync(
        TopCutDbContext db,
        string? viewerUserId,
        Guid storeId,
        CancellationToken cancellationToken) =>
        viewerUserId is null
            ? null
            : await db.StorePlayers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(player => player.UserId == viewerUserId
                    && player.TenantId == storeId
                    && player.MergedIntoId == null)
                .Select(player => (Guid?)player.Id)
                .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The league's tournaments that anyone may see. The tenant filter is off and
    /// <see cref="PublicVisibility.OnlyPublic"/> takes its place, exactly as in the public search.
    /// </summary>
    private static IQueryable<Tournament> Visible(TopCutDbContext db, Guid leagueId) =>
        db.Tournaments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OnlyPublic(db)
            .Where(tournament => tournament.LeagueId == leagueId);
}
