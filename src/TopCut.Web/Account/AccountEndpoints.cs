using Microsoft.AspNetCore.Authentication;
using TopCut.Application.Abstractions;

namespace TopCut.Web.Account;

/// <summary>
/// The parts of the account flow that are redirects rather than pages: signing out, and handing off
/// to Google and back. Everything else is a Razor component under <c>Components/Account</c>.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder routes)
    {
        var account = routes.MapGroup("/cuenta");

        account.MapPost("/salir", async (ISignInService signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.LocalRedirect("/");
        });

        account.MapPost("/google", (HttpContext http) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = "/cuenta/google/vuelta" };
            return Results.Challenge(properties, ["Google"]);
        });

        account.MapGet("/google/vuelta", async (IExternalSignInService external) =>
        {
            var result = await external.TrySignInAsync();

            return result.Outcome switch
            {
                ExternalSignInOutcome.SignedIn => Results.LocalRedirect("/"),
                ExternalSignInOutcome.NeedsConsent => Results.LocalRedirect("/cuenta/google/alta"),
                _ => Results.LocalRedirect("/cuenta/acceso?error=google")
            };
        });

        return routes;
    }
}
