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
            events);
    }

    public async Task<bool> IsPublicAsync(Guid leagueId, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        return await Visible(db, leagueId).AnyAsync(cancellationToken);
    }

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
