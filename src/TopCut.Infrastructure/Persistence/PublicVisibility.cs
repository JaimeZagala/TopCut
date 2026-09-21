using Microsoft.EntityFrameworkCore;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// What a tournament has to be for anyone outside its store to see it (ADR-0029).
/// <para>
/// One definition, used by the public search and by the player signing up from it. Two copies of
/// this rule would eventually stop agreeing, and the way that failure shows up is a tournament you
/// can read but not join, or worse, one you can join but not read.
/// </para>
/// </summary>
internal static class PublicVisibility
{
    /// <summary>
    /// Narrows a query to the publicly visible tournaments. The caller has already turned the
    /// tenant filter off with <c>IgnoreQueryFilters</c>; this is what takes its place.
    /// <list type="bullet">
    /// <item>the organiser <b>published</b> it — a draft is the store's own workspace, and a
    /// cancelled one stays visible on purpose, because somebody had it in their diary;</item>
    /// <item>the store is <b>approved</b> right now, so revoking one takes its events off the
    /// listing on the next request rather than whenever a cache feels like it;</item>
    /// <item>and it is <b>not a test store</b>, which is what stands in for the staging environment
    /// the Azure free tier cannot fund.</item>
    /// </list>
    /// </summary>
    public static IQueryable<Tournament> OnlyPublic(this IQueryable<Tournament> tournaments, TopCutDbContext db) =>
        tournaments.Where(tournament =>
            tournament.Status != TournamentStatus.Draft
            && db.Stores.Any(store =>
                store.Id == tournament.TenantId
                && store.Status == StoreStatus.Approved
                && !store.IsTestStore));
}
