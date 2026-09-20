using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using TopCut.Application.Abstractions;

namespace TopCut.Api.Auth;

/// <summary>
/// Resolves the current store from the signed-in principal. The tenant travels as a claim, so no
/// database round trip is needed on every request; whether that store may act is a separate check.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? TenantId =>
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
