using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TopCut.Application.Abstractions;

namespace TopCut.Api.Auth;

/// <param name="ConsentAccepted">The data-protection notice. Registration is refused without it.</param>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    bool ConsentAccepted);

public sealed record LoginRequest(string Email, string Password);

public sealed record StoreApplicationRequest(
    string StoreName,
    string Street,
    string City,
    string Province,
    string PostalCode);

/// <summary>
/// Authentication and store onboarding over HTTP, for external clients. The Blazor UI does not go
/// through these: it calls the same <c>TopCut.Application</c> services in process.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // JSON only, so the antiforgery middleware has nothing to validate, and bearer only, so a
        // cross-site form post carrying the user's session cookie cannot reach these endpoints.
        // The Blazor UI never calls them: it invokes the same application services in process.
        var auth = routes.MapGroup("/api/v1/auth").WithTags("Auth").DisableAntiforgery();

        auth.MapPost("/register", async (
            RegisterRequest request,
            IAccountService accounts,
            CancellationToken cancellationToken) =>
        {
            var result = await accounts.RegisterPlayerAsync(
                new RegistrationRequest(request.Email, request.Password, request.DisplayName, request.ConsentAccepted),
                cancellationToken);

            return result.Succeeded
                ? Results.Created($"/api/v1/auth/me", new { userId = result.UserId })
                : Problem(result.Errors, StatusCodes.Status400BadRequest);
        })
        .AllowAnonymous()
        .WithSummary("Registra un jugador. Exige aceptar el aviso de proteccion de datos.");

        auth.MapPost("/login", async (
            LoginRequest request,
            ISignInService signIn,
            CancellationToken cancellationToken) =>
        {
            var result = await signIn.SignInWithBearerAsync(request.Email, request.Password, cancellationToken);

            // On success the bearer handler has already written the access token to the response,
            // so the endpoint itself returns nothing.
            return result.Succeeded
                ? Results.Empty
                : Problem(result.Errors, StatusCodes.Status401Unauthorized);
        })
        .AllowAnonymous()
        .WithSummary("Devuelve un token Bearer para clientes externos.");

        auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            email = user.FindFirstValue(ClaimTypes.Email),
            displayName = user.FindFirstValue(ClaimTypes.GivenName),
            roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            tenantId = user.FindFirstValue(TopCutClaims.Tenant)
        }))
        .RequireAuthorization(BearerOnly)
        .WithSummary("Quien soy, con mis roles y mi tienda si la tengo.");

        var stores = routes.MapGroup("/api/v1/stores").WithTags("Stores").DisableAntiforgery();

        stores.MapPost("/apply", async (
            StoreApplicationRequest request,
            ClaimsPrincipal user,
            IStoreOnboarding onboarding,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var result = await onboarding.ApplyAsync(
                userId,
                new StoreApplication(request.StoreName, request.Street, request.City, request.Province, request.PostalCode),
                cancellationToken);

            return result.Succeeded ? Results.Accepted() : Problem(result.Errors, StatusCodes.Status400BadRequest);
        })
        .RequireAuthorization(BearerOnly)
        .WithSummary("Solicita el rol de tienda. Queda pendiente de que un Superadmin la apruebe.");

        stores.MapPost("/{storeId:guid}/approve", async (
            Guid storeId,
            IStoreOnboarding onboarding,
            CancellationToken cancellationToken) =>
        {
            var result = await onboarding.ApproveAsync(storeId, cancellationToken);
            return result.Succeeded ? Results.NoContent() : Problem(result.Errors, StatusCodes.Status400BadRequest);
        })
        .RequireAuthorization(BearerSuperadmin)
        .WithSummary("Activa una tienda para que pueda crear y publicar torneos.");

        stores.MapPost("/{storeId:guid}/revoke", async (
            Guid storeId,
            IStoreOnboarding onboarding,
            CancellationToken cancellationToken) =>
        {
            var result = await onboarding.RevokeAsync(storeId, cancellationToken);
            return result.Succeeded ? Results.NoContent() : Problem(result.Errors, StatusCodes.Status400BadRequest);
        })
        .RequireAuthorization(BearerSuperadmin)
        .WithSummary("Retira el permiso de organizar a una tienda.");

        return routes;
    }

    /// <summary>
    /// Authenticated by bearer token and nothing else. Named policies accept the session cookie too,
    /// because the Blazor pages need them, but over HTTP that would mean a cross-site request could
    /// ride on the user's cookie.
    /// </summary>
    private static void BearerOnly(AuthorizationPolicyBuilder policy) => policy
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme)
        .RequireAuthenticatedUser();

    private static void BearerSuperadmin(AuthorizationPolicyBuilder policy) => policy
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme)
        .RequireRole(TopCutRoles.Superadmin);

    /// <summary>RFC 9457 problem details, which is what ASP.NET Core produces by default.</summary>
    private static IResult Problem(IReadOnlyList<string> errors, int statusCode) => Results.Problem(
        detail: string.Join(" ", errors),
        statusCode: statusCode);
}
