using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Identity;

/// <summary>
/// Adds the tenant claim when a store account signs in. Putting the store on the principal is what
/// lets <c>ITenantContext</c> resolve without a database round trip on every request.
/// <para>
/// The claim only says which store the account belongs to. Whether that store is <b>allowed</b> to
/// organise is checked separately against the database, so revoking a store takes effect on the
/// next request instead of whenever the cookie happens to expire.
/// </para>
/// </summary>
public sealed class TopCutUserClaimsPrincipalFactory(
    UserManager<TopCutUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<TopCutUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TopCutUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (user.StoreId is { } storeId)
        {
            identity.AddClaim(new Claim(TopCutClaims.Tenant, storeId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        }

        return identity;
    }
}
