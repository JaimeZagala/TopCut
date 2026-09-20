using Microsoft.AspNetCore.Identity;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Identity;

/// <summary>
/// Creates the three roles and, if configured, the bootstrap Superadmin. Without this last step
/// nobody could ever approve the first store: it is a chicken-and-egg that has to be broken from
/// outside the application.
/// <para>Idempotent, so it is safe to run on every startup.</para>
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roles)
    {
        foreach (var role in TopCutRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }
    }

    /// <summary>
    /// Creates the first Superadmin from configuration if that email has no account yet. The
    /// password comes from configuration — user secrets locally, an app setting in Azure — and is
    /// never committed.
    /// </summary>
    public static async Task<IdentityResult?> SeedSuperadminAsync(
        UserManager<TopCutUser> users,
        string? email,
        string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        if (await users.FindByEmailAsync(email) is not null)
        {
            return null;
        }

        var superadmin = new TopCutUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Superadmin",
            ConsentAcceptedAt = DateTimeOffset.UtcNow,
            ConsentVersion = PrivacyNotice.CurrentVersion
        };

        var created = await users.CreateAsync(superadmin, password);

        if (created.Succeeded)
        {
            await users.AddToRoleAsync(superadmin, TopCutRoles.Superadmin);
        }

        return created;
    }
}
