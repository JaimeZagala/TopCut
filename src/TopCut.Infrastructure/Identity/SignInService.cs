using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TopCut.Application.Abstractions;
using TopCut.Domain.Tenancy;
using TopCut.Infrastructure.Persistence;

namespace TopCut.Infrastructure.Identity;

public sealed class SignInService(SignInManager<TopCutUser> signInManager) : ISignInService
{
    public Task<OperationResult> SignInWithBearerAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        SignIn(email, password, rememberMe: false, IdentityConstants.BearerScheme);

    public Task<OperationResult> SignInWithCookieAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default) =>
        SignIn(email, password, rememberMe, IdentityConstants.ApplicationScheme);

    public Task SignOutAsync() => signInManager.SignOutAsync();

    private async Task<OperationResult> SignIn(string email, string password, bool rememberMe, string scheme)
    {
        // Setting the scheme is what decides whether the sign-in produces a cookie or an access
        // token: SignInManager hands off to whichever handler is registered for it.
        signInManager.AuthenticationScheme = scheme;

        var result = await signInManager.PasswordSignInAsync(
            email,
            password,
            isPersistent: rememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return OperationResult.Ok();
        }

        // Deliberately vague: saying "no such account" would let anyone enumerate registered emails.
        return result.IsLockedOut
            ? OperationResult.Fail("La cuenta esta bloqueada temporalmente por intentos fallidos.")
            : OperationResult.Fail("Email o contrasena incorrectos.");
    }
}

/// <summary>
/// Reads store approval on its own <see cref="TopCutDbContext"/>, in a scope of its own.
/// <para>
/// It cannot borrow the request's context: the authorization policy runs from every
/// <c>AuthorizeView</c> on the page as well as from the endpoint, Blazor renders those in parallel,
/// and a DbContext used by two of them at once throws "a second operation was started on this
/// context instance". The query is a single indexed read, so a short-lived context is cheap.
/// </para>
/// </summary>
public sealed class StoreDirectory(IServiceScopeFactory scopes) : IStoreDirectory
{
    public async Task<bool> IsApprovedAsync(Guid storeId, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TopCutDbContext>();

        // Crosses the tenant boundary on purpose: this is the check that decides the tenant's own
        // permissions, so it cannot depend on the filter it is about to enable (ADR-0011).
        return await db.Stores
            .IgnoreQueryFilters()
            .AnyAsync(store => store.Id == storeId && store.Status == StoreStatus.Approved, cancellationToken);
    }
}
