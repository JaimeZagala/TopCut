namespace TopCut.Application.Abstractions;

/// <param name="ConsentAccepted">
/// Whether the user ticked the data-protection notice. Registration is refused without it, and the
/// check lives on the server so it cannot be skipped by posting straight to the API.
/// </param>
public sealed record RegistrationRequest(
    string Email,
    string Password,
    string DisplayName,
    bool ConsentAccepted);

/// <param name="Errors">Messages already fit to show the user, in Spanish.</param>
public sealed record OperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static OperationResult Ok() => new(true, []);

    public static OperationResult Fail(params string[] errors) => new(false, errors);

    public static OperationResult Fail(IEnumerable<string> errors) => new(false, [.. errors]);
}

public sealed record RegistrationResult(bool Succeeded, string? UserId, IReadOnlyList<string> Errors);

/// <summary>Registering accounts. Players sign up freely; no approval is involved.</summary>
public interface IAccountService
{
    Task<RegistrationResult> RegisterPlayerAsync(
        RegistrationRequest request,
        CancellationToken cancellationToken = default);
}

/// <param name="Street">Structured from the start, so "tournaments near me" is a later query and not a redesign.</param>
public sealed record StoreApplication(
    string StoreName,
    string Street,
    string City,
    string Province,
    string PostalCode);

/// <summary>
/// Turning an ordinary account into an organising store, and the Superadmin's control over it.
/// Anyone can apply; nobody organises until a Superadmin approves, which is the cost control
/// described in docs/vision.md.
/// </summary>
public interface IStoreOnboarding
{
    /// <summary>Creates the store, pending approval, and links it to the applicant's account.</summary>
    Task<OperationResult> ApplyAsync(
        string userId,
        StoreApplication application,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ApproveAsync(Guid storeId, CancellationToken cancellationToken = default);

    Task<OperationResult> RevokeAsync(Guid storeId, CancellationToken cancellationToken = default);
}
