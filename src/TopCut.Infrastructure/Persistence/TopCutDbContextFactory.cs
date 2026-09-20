using Microsoft.EntityFrameworkCore;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// A fresh <see cref="TopCutDbContext"/> per operation (ADR-0023).
/// <para>
/// A DbContext refuses to run two operations at once, and Blazor renders the components of a screen
/// in parallel, so sharing one context for the whole request or circuit breaks as soon as a page
/// has more than one thing to read. Each context is cheap; the connection underneath is pooled.
/// </para>
/// <para>
/// The <b>same</b> <see cref="ITenantContext"/> instance is handed to every context it makes, so
/// each one is scoped to the same store. That matters: creating contexts from a child DI scope
/// instead would hand them a fresh, empty tenant context, and the filter — which fails closed
/// (ADR-0011) — would quietly return nothing.
/// </para>
/// </summary>
public sealed class TopCutDbContextFactory(
    DbContextOptions<TopCutDbContext> options,
    ITenantContext tenantContext)
    : IDbContextFactory<TopCutDbContext>
{
    public TopCutDbContext CreateDbContext() => new(options, tenantContext);
}
