using System.Globalization;
using System.Text;
using TopCut.Domain.Common;

namespace TopCut.Domain.Tenancy;

/// <summary>
/// A person in a store's book of regulars, as opposed to a <c>Participant</c>, which is that person
/// in <b>one</b> tournament.
/// <para>
/// This exists because a league has to add up what somebody scored across several events, and until
/// now there was nothing to add it up <i>to</i>: a walk-in was a name typed at the counter, and two
/// "Ana" in two tournaments might or might not be the same Ana. Guessing by name would quietly merge
/// two people or split one; the store, which knows its regulars by sight, decides instead.
/// </para>
/// </summary>
public class StorePlayer : BusinessEntity, ITenantOwned, IAuditable
{
    private StorePlayer() { }

    public StorePlayer(Guid tenantId, string name, string? userId = null)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El jugador necesita un nombre.");

        TenantId = tenantId;
        Name = name.Trim();
        NormalizedName = Normalize(name);
        UserId = userId;
    }

    public Guid TenantId { get; private set; }

    /// <summary>As the store writes it, accents and all. This is what goes on the pairings.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The name with accents and case stripped, kept as a column so the database can enforce one
    /// entry per person per store and the counter's autocomplete can match "ana perez".
    /// </summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>
    /// Their account, once there is one. It is the strong link: a player who signs up online is
    /// recognised by this and never by their name.
    /// </summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Folded into another entry by the store. Kept rather than deleted so the participants that
    /// already point here keep pointing somewhere, and so a merge can be explained.
    /// </summary>
    public Guid? MergedIntoId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsMerged => MergedIntoId is not null;

    public void Rename(string name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El jugador necesita un nombre.");

        Name = name.Trim();
        NormalizedName = Normalize(name);
    }

    /// <summary>
    /// Ties an account to somebody the store had been writing in by hand. Once tied it stays tied:
    /// moving an account between people would silently move their league points with it.
    /// </summary>
    public void LinkAccount(string userId)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(userId), "Hace falta una cuenta.");
        DomainException.Require(UserId is null, "Este jugador ya tiene una cuenta vinculada.");

        UserId = userId;
    }

    /// <summary>Marks this entry as folded into another. The moving of rows is the caller's job.</summary>
    public void MergeInto(Guid survivorId)
    {
        DomainException.Require(survivorId != Id, "Un jugador no se puede fusionar consigo mismo.");
        DomainException.Require(!IsMerged, "Este jugador ya se fusionó con otro.");

        MergedIntoId = survivorId;
    }

    /// <summary>
    /// Lower case, no accents, single spaces. Enough to stop "Ana Pérez" and "ana perez " being two
    /// people in the book, and deliberately no cleverer than that: anything fuzzier would start
    /// merging players who really are different.
    /// </summary>
    public static string Normalize(string name)
    {
        var trimmed = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var stripped = trimmed
            .Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);

        return string.Concat(stripped)
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}
