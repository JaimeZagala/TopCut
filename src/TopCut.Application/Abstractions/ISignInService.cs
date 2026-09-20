namespace TopCut.Application.Abstractions;

/// <summary>
/// Signing in. Two schemes on purpose: the Blazor UI uses a session cookie, and external clients —
/// the future Android app — use bearer tokens (see docs/arquitectura.md).
/// </summary>
public interface ISignInService
{
    /// <summary>Signs in and lets the bearer handler write the access token to the response.</summary>
    Task<OperationResult> SignInWithBearerAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Signs in and issues the session cookie the Blazor UI uses.</summary>
    Task<OperationResult> SignInWithCookieAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default);

    Task SignOutAsync();
}

/// <summary>
/// Reads store status without exposing the persistence layer to presentation. Used by the
/// authorization policy that decides whether an account may organise <i>right now</i>.
/// </summary>
public interface IStoreDirectory
{
    Task<bool> IsApprovedAsync(Guid storeId, CancellationToken cancellationToken = default);
}
