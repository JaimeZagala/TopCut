namespace TopCut.Application.Abstractions;

/// <summary>
/// The store whose data the current operation is allowed to see. Resolved per request from the
/// signed-in user; the EF Core global query filter reads it so no use case has to filter by hand.
/// <para>
/// <c>null</c> means no store scope: anonymous public browsing, the Superadmin acting across the
/// platform, or a background job. Queries that must reach across tenants opt out explicitly with
/// <c>IgnoreQueryFilters()</c>, so crossing the boundary is always visible in the code.
/// </para>
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}

/// <summary>
/// Establishes the store scope where it cannot be read from the incoming request.
/// <para>
/// Interactive Blazor pages run inside a SignalR circuit, and a circuit has <b>no HttpContext</b>:
/// left to itself the tenant would resolve to <c>null</c> and, because the filter fails closed
/// (ADR-0011), the store would see none of its own data. The store's layout sets it once from the
/// authenticated principal, which is the same claim the HTTP path reads (ADR-0024).
/// </para>
/// </summary>
public interface ITenantScope
{
    void Set(Guid? tenantId);
}
