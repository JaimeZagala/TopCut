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
    /// Both schemes are accepted everywhere: the Blazor UI arrives with a session cookie and the
    /// future Android app with a bearer token, and neither should need its own set of policies.
    /// </summary>
    private static readonly string[] BothSchemes =
        [IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme];

    public static IServiceCollection AddTopCutApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IAuthorizationHandler, ApprovedStoreHandler>();

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        authentication.AddIdentityCookies();
        authentication.AddBearerToken(IdentityConstants.BearerScheme);
        authentication.AddGoogleIfConfigured(configuration);

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(BothSchemes)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(TopCutPolicies.IsSuperadmin, policy => policy
                .AddAuthenticationSchemes(BothSchemes)
                .RequireRole(TopCutRoles.Superadmin))
            .AddPolicy(TopCutPolicies.IsPlayer, policy => policy
                .AddAuthenticationSchemes(BothSchemes)
                .RequireRole(TopCutRoles.Player))
            .AddPolicy(TopCutPolicies.CanOrganize, policy => policy
                .AddAuthenticationSchemes(BothSchemes)
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
