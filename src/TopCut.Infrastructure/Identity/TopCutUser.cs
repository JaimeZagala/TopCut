using Microsoft.AspNetCore.Identity;

namespace TopCut.Infrastructure.Identity;

/// <summary>
/// An application account. Note that a <c>Participant</c> does <b>not</b> need one: the store signs
/// up walk-ins by hand and those players never have a user row.
/// </summary>
public class TopCutUser : IdentityUser
{
    /// <summary>Name shown on pairings and standings when this account joins a tournament.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When the user accepted the data-protection notice. Registration is refused without it, so on
    /// an existing account it is always set.
    /// </summary>
    public DateTimeOffset ConsentAcceptedAt { get; set; }

    /// <summary>
    /// Which version of the notice they accepted. Kept so that a future change to the text can be
    /// re-consented without guessing who had seen what.
    /// </summary>
    public string ConsentVersion { get; set; } = string.Empty;

    /// <summary>
    /// The store this account organises for. Null for players and for the Superadmin. It becomes
    /// the tenant claim, which is what scopes every query.
    /// </summary>
    public Guid? StoreId { get; set; }
}
