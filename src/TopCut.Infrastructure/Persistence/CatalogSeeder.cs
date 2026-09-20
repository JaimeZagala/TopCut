using Microsoft.EntityFrameworkCore;
using TopCut.Domain.Catalog;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Puts the v1 TCG catalog in place. Written as a seeder rather than <c>HasData</c> because the
/// rulesets carry owned types, which <c>HasData</c> handles poorly, and because this way the seed
/// goes through the same domain constructors the application uses.
/// <para>Idempotent: it only creates what is missing, so it is safe to run on every startup.</para>
/// </summary>
public static class CatalogSeeder
{
    public static async Task SeedAsync(TopCutDbContext db, CancellationToken cancellationToken = default)
    {
        await EnsureTcgAsync(db, SeedCatalog.Magic(), SeedCatalog.MagicRuleset, cancellationToken);
        await EnsureTcgAsync(db, SeedCatalog.OnePiece(), SeedCatalog.OnePieceRuleset, cancellationToken);
    }

    private static async Task EnsureTcgAsync(
        TopCutDbContext db,
        Tcg tcg,
        Func<int, Ruleset> rulesetFor,
        CancellationToken cancellationToken)
    {
        var existing = await db.Tcgs
            .Include(t => t.Ruleset)
            .SingleOrDefaultAsync(t => t.Code == tcg.Code, cancellationToken);

        if (existing is null)
        {
            db.Tcgs.Add(tcg);
            await db.SaveChangesAsync(cancellationToken);

            db.Rulesets.Add(rulesetFor(tcg.Id));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // A TCG that somehow lost its ruleset would break every tournament created from it.
        if (existing.Ruleset is null)
        {
            db.Rulesets.Add(rulesetFor(existing.Id));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Backfill for rulesets created before a field existed. Only fills what is empty, so a
        // table the Superadmin has since tuned is never overwritten by the seed.
        if (existing.Ruleset.AttendanceBands.Count == 0)
        {
            existing.Ruleset.SetAttendanceBands(rulesetFor(existing.Id).AttendanceBands);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
