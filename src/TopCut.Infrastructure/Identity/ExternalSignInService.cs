using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Identity;

public sealed class ExternalSignInService(
    SignInManager<TopCutUser> signInManager,
    UserManager<TopCutUser> users,
    TimeProvider clock) : IExternalSignInService
{
    public async Task<ExternalSignInResult> TrySignInAsync(CancellationToken cancellationToken = default)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();

        if (info is null)
        {
            return new ExternalSignInResult(
                ExternalSignInOutcome.Failed,
                null,
                ["No se recibio informacion del proveedor externo."]);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        var signedIn = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        if (signedIn.Succeeded)
        {
            return new ExternalSignInResult(ExternalSignInOutcome.SignedIn, email, []);
        }

        if (email is null)
        {
            return new ExternalSignInResult(
                ExternalSignInOutcome.Failed,
                null,
                ["El proveedor no facilito un email."]);
        }

        // Someone who registered with email and password and now signs in with Google: link the
        // provider to the account they already have instead of creating a second one.
        var existing = await users.FindByEmailAsync(email);

        if (existing is not null)
        {
            var linked = await users.AddLoginAsync(existing, info);

            if (!linked.Succeeded)
            {
                return new ExternalSignInResult(
                    ExternalSignInOutcome.Failed,
                    email,
                    [.. linked.Errors.Select(e => e.Description)]);
            }

            await signInManager.SignInAsync(existing, isPersistent: false);
            return new ExternalSignInResult(ExternalSignInOutcome.SignedIn, email, []);
        }

        // Brand new: no account is created until the notice is accepted.
        return new ExternalSignInResult(ExternalSignInOutcome.NeedsConsent, email, []);
    }

    public async Task<OperationResult> CompleteRegistrationAsync(
        string displayName,
        bool consentAccepted,
        CancellationToken cancellationToken = default)
    {
        if (!consentAccepted)
        {
            return OperationResult.Fail("Hay que aceptar el aviso de protección de datos.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return OperationResult.Fail("Indica el nombre con el que quieres aparecer.");
        }

        var info = await signInManager.GetExternalLoginInfoAsync();

        if (info is null)
        {
            return OperationResult.Fail("La sesión con el proveedor externo ha caducado. Vuelve a intentarlo.");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        if (email is null)
        {
            return OperationResult.Fail("El proveedor no facilito un email.");
        }

        var user = new TopCutUser
        {
            UserName = email,
            Email = email,
            // The provider already verified it, so there is nothing for us to confirm.
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            ConsentAcceptedAt = clock.GetUtcNow(),
            ConsentVersion = PrivacyNotice.CurrentVersion
        };

        var created = await users.CreateAsync(user);

        if (!created.Succeeded)
        {
            return OperationResult.Fail(created.Errors.Select(e => e.Description));
        }

        await users.AddToRoleAsync(user, TopCutRoles.Player);
        await users.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: false);

        return OperationResult.Ok();
    }
}
