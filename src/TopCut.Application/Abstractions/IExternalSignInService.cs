namespace TopCut.Application.Abstractions;

public enum ExternalSignInOutcome
{
    /// <summary>Signed in: the account already existed and was linked to the provider.</summary>
    SignedIn = 1,

    /// <summary>
    /// First time with this provider and no account to link to. The data-protection notice has not
    /// been accepted yet, so the account is not created until the user does.
    /// </summary>
    NeedsConsent = 2,

    Failed = 3
}

public sealed record ExternalSignInResult(ExternalSignInOutcome Outcome, string? Email, IReadOnlyList<string> Errors);

/// <summary>
/// Signing in through an external provider, Google in v1. Kept behind an interface so presentation
/// never touches Identity types directly.
/// </summary>
public interface IExternalSignInService
{
    /// <summary>Signs in if the provider account is already known or matches an existing email.</summary>
    Task<ExternalSignInResult> TrySignInAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the account for a provider login that had none, once consent is given, and links the
    /// two so next time <see cref="TrySignInAsync"/> is enough.
    /// </summary>
    Task<OperationResult> CompleteRegistrationAsync(
        string displayName,
        bool consentAccepted,
        CancellationToken cancellationToken = default);
}
