using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TopCut.Application.Abstractions;

namespace TopCut.Api.Auth;

/// <summary>
/// Resolves the current store from the signed-in principal. The tenant travels as a claim, so no
/// database round trip is needed on every request; whether that store may act is a separate check.
/// </summary>
public sealed class TenantContext(IHttpContextAccessor accessor) : ITenantContext, ITenantScope
{
    private Guid? _scoped;
    private bool _hasScope;

    public Guid? TenantId => _hasScope ? _scoped : FromRequest();

    /// <summary>
    /// Used where there is no request to read from — an interactive Blazor page runs inside a
    /// SignalR circuit, and a circuit has no <c>HttpContext</c> (ADR-0024). The caller takes the
    /// value from the authenticated principal, which is the very same claim <see cref="FromRequest"/>
    /// reads, so this widens nobody's access: it only stops the scope from vanishing.
    /// </summary>
    public void Set(Guid? tenantId)
    {
        _scoped = tenantId;
        _hasScope = true;
    }

    private Guid? FromRequest() =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(TopCutClaims.Tenant)?.Value, out var tenantId)
            ? tenantId
            : null;
}

/// <summary>Holds the Store role and belongs to a store that is approved at this moment.</summary>
public sealed class ApprovedStoreRequirement : IAuthorizationRequirement;

/// <summary>
/// Checks store approval against the database rather than trusting a claim. A claim would be
/// baked into the cookie, so revoking a store would not take effect until the user signed out; this
/// way it takes effect on the next request.
/// </summary>
public sealed class ApprovedStoreHandler(IStoreDirectory stores)
    : AuthorizationHandler<ApprovedStoreRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovedStoreRequirement requirement)
    {
        if (!context.User.IsInRole(TopCutRoles.Store))
        {
            return;
        }

        var claim = context.User.FindFirst(TopCutClaims.Tenant)?.Value;

        if (!Guid.TryParse(claim, out var storeId))
        {
            return;
        }

        if (await stores.IsApprovedAsync(storeId))
        {
            context.Succeed(requirement);
        }
    }
}
