using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TopCut.Api.Auth;
using TopCut.Application.Abstractions;

namespace TopCut.Api;

/// <summary>
/// Wires up authentication, authorization and the HTTP surface. <c>TopCut.Web</c> calls this: the
/// two ship as one process and one deployment (ADR-0004).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// The named policies belong to the browser, so they authenticate with the session cookie and
    /// nothing else (ADR-0020). External clients arrive by bearer token and every API endpoint says
    /// so explicitly (ADR-0013), which keeps each door to exactly one key.
    /// <para>
    /// Listing both schemes on one policy looked tidier but was not: a rejected page challenged
    /// both handlers, and the answer came back as a 401 <i>with</i> a redirect header attached,
    /// which no browser can do anything sensible with.
    /// </para>
    /// </summary>
    private static readonly string[] BrowserScheme = [IdentityConstants.ApplicationScheme];

    public static IServiceCollection AddTopCutApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        // One instance serving both faces: the query filter reads it, and a Blazor circuit — which
        // has no HttpContext to read from — sets it (ADR-0024).
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<IAuthorizationHandler, ApprovedStoreHandler>();

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        // Where the cookie handler sends someone who is missing a session or a permission. Without
        // this it would aim at the ASP.NET template's own /Account/Login, which does not exist here.
        authentication.AddIdentityCookies(cookies => cookies.ApplicationCookie?.Configure(options =>
        {
            options.LoginPath = "/cuenta/acceso";
            options.AccessDeniedPath = "/sin-permiso";
            options.ReturnUrlParameter = "volver";
        }));

        authentication.AddBearerToken(IdentityConstants.BearerScheme);
        authentication.AddGoogleIfConfigured(configuration);

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(BrowserScheme)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(TopCutPolicies.IsSuperadmin, policy => policy
                .AddAuthenticationSchemes(BrowserScheme)
                .RequireRole(TopCutRoles.Superadmin))
            .AddPolicy(TopCutPolicies.IsPlayer, policy => policy
                .AddAuthenticationSchemes(BrowserScheme)
                .RequireRole(TopCutRoles.Player))
            .AddPolicy(TopCutPolicies.CanOrganize, policy => policy
                .AddAuthenticationSchemes(BrowserScheme)
                .AddRequirements(new ApprovedStoreRequirement()));

        services.AddProblemDetails();

        return services;
    }

    public static IEndpointRouteBuilder MapTopCutApi(this IEndpointRouteBuilder routes)
    {
        routes.MapAuthEndpoints();
        return routes;
    }

    /// <summary>
    /// Google sign-in only appears when credentials are configured. That keeps the application
    /// bootable — and the whole milestone unblocked — before the Google Cloud project exists.
    /// </summary>
    public static bool IsGoogleConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    private static void AddGoogleIfConfigured(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        if (!IsGoogleConfigured(configuration))
        {
            return;
        }

        authentication.AddGoogle(options =>
        {
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
    }
}
