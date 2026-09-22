namespace TopCut.Application.Abstractions;

/// <summary>
/// The three access levels from docs/vision.md, as Identity roles. Strings live here, in a layer
/// with no ASP.NET dependency, so use cases and presentation can both name them without guessing.
/// </summary>
public static class TopCutRoles
{
    /// <summary>Platform owner: global settings, the TCG catalog, and approving stores.</summary>
    public const string Superadmin = "Superadmin";

    /// <summary>An organising store. Useless until a Superadmin approves it.</summary>
    public const string Store = "Store";

    /// <summary>A player. Free registration, no approval.</summary>
    public const string Player = "Player";

    public static readonly IReadOnlyList<string> All = [Superadmin, Store, Player];
}

/// <summary>
/// Authorization policies. Roles say who someone is; policies say what the application will let
/// them do, which is not the same thing: a store account that has been revoked still holds the
/// Store role but must not be able to publish anything.
/// </summary>
public static class TopCutPolicies
{
    public const string IsSuperadmin = "topcut:superadmin";

    /// <summary>Holds the Store role <b>and</b> its store is currently approved.</summary>
    public const string CanOrganize = "topcut:can-organize";

    public const string IsPlayer = "topcut:player";
}

/// <summary>
/// Claims TopCut adds on top of the standard ones.
/// </summary>
public static class TopCutClaims
{
    /// <summary>The store a user belongs to. This is what scopes every query to one tenant.</summary>
    public const string Tenant = "topcut:tenant";
}

/// <summary>
/// The data-protection notice shown at registration. Versioned so that, if the text ever changes
/// in a way that matters, it is possible to tell who accepted which version and ask again.
/// </summary>
public static class PrivacyNotice
{
    public const string CurrentVersion = "2026-09-23";
}
