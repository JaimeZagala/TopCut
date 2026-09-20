using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;
using TopCut.Domain.Catalog;
using TopCut.Domain.Common;
using TopCut.Domain.Leagues;
using TopCut.Domain.Tenancy;
using TopCut.Domain.Tournaments;
using TopCut.Infrastructure.Identity;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// The single shared database behind every store. Multi-tenancy is a <c>TenantId</c> column plus
/// global query filters, which is the cheapest approach that still isolates stores properly and is
/// the only one that fits the Azure SQL free tier's one-database-per-subscription limit
/// (see docs/arquitectura.md).
/// </summary>
public class TopCutDbContext(DbContextOptions<TopCutDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<TopCutUser>(options)
{
    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(TopCutDbContext)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Read by the global query filters. EF Core re-evaluates it per query against the live context,
    /// so a scoped context created for another store sees that store's rows and no others.
    /// </summary>
    public Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Tcg> Tcgs => Set<Tcg>();

    public DbSet<Ruleset> Rulesets => Set<Ruleset>();

    public DbSet<Tournament> Tournaments => Set<Tournament>();

    public DbSet<Participant> Participants => Set<Participant>();

    public DbSet<Round> Rounds => Set<Round>();

    public DbSet<Pairing> Pairings => Set<Pairing>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<League> Leagues => Set<League>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity brings its own tables (AspNetUsers, AspNetRoles...) with string keys, which is
        // the framework default and not worth changing (ADR-0006).
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TopCutDbContext).Assembly);

        // One filter per tenant-owned entity, applied by convention rather than by hand, so adding
        // an entity later cannot silently leak across stores just because someone forgot a line.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(null, [modelBuilder, this]);
            }
        }
    }

    private static void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder, TopCutDbContext context)
        where TEntity : class, ITenantOwned
    {
        // Fail closed: with no tenant in scope the comparison is against NULL and matches nothing.
        // Anything that genuinely needs to read across stores (public tournament search, the
        // Superadmin panel) opts out with IgnoreQueryFilters(), so crossing the boundary is always
        // visible at the call site instead of being the accidental default.
        Expression<Func<TEntity, bool>> filter = entity => entity.TenantId == context.CurrentTenantId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}
