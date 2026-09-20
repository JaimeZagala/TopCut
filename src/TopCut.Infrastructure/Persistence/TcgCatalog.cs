using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Catalog;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The TCG catalog as the store's screens see it. Global data, not owned by any store, so there is
/// no tenant filter to work around here.
/// </summary>
public sealed class TcgCatalog(IDbContextFactory<TopCutDbContext> contexts) : ITcgCatalog
{
    public async Task<IReadOnlyList<TcgOption>> ActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        var rulesets = await db.Rulesets
            .AsNoTracking()
            .Include(ruleset => ruleset.Tcg)
            .Where(ruleset => ruleset.Tcg.IsActive)
            .OrderBy(ruleset => ruleset.Tcg.Name)
            .ToListAsync(cancellationToken);

        return
        [
            .. rulesets.Select(ruleset => new TcgOption(
                ruleset.Tcg.Id,
                ruleset.Tcg.Code,
                ruleset.Tcg.Name,
                ruleset.SwissPhase,
                ruleset.TopCutPhase,
                ruleset.TimeExpiry,
                ruleset.AttendanceBands))
        ];
    }

    public async Task<Ruleset?> ActiveRulesetForAsync(int tcgId, CancellationToken cancellationToken = default)
    {
        await using var db = contexts.CreateDbContext();

        // Detached on purpose. The caller hands it to Tournament.Create, which only reads it: the
        // tournament copies the values and keeps no reference back, so nothing here must end up
        // attached to the save that creates the tournament.
        return await db.Rulesets
            .AsNoTracking()
            .Include(ruleset => ruleset.Tcg)
            .SingleOrDefaultAsync(ruleset => ruleset.TcgId == tcgId && ruleset.Tcg.IsActive, cancellationToken);
    }
}
