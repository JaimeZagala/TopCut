using TopCut.Domain.Common;

namespace TopCut.Domain.Catalog;

/// <summary>
/// A card game in the global catalog. Managed by the Superadmin: switching on a new TCG means
/// adding its row and its <see cref="Ruleset"/>, not writing code (see docs/vision.md).
/// </summary>
public class Tcg : CatalogEntity
{
    private Tcg() { }

    public Tcg(string code, string name, bool isActive = true)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(code), "El TCG necesita un código.");
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El TCG necesita un nombre.");

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        IsActive = isActive;
    }

    /// <summary>Short, stable code used in URLs and search filters (for example <c>MTG</c>).</summary>
    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>A deactivated TCG stops being offered for new tournaments; existing ones are untouched.</summary>
    public bool IsActive { get; private set; }

    public Ruleset Ruleset { get; private set; } = null!;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Rename(string name)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "El TCG necesita un nombre.");
        Name = name.Trim();
    }
}
