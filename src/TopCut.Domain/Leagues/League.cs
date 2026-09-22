using TopCut.Domain.Common;

namespace TopCut.Domain.Leagues;

/// <summary>
/// A seasonal league: groups several of a store's tournaments and accumulates their points using
/// the same scoring model as a one-off tournament. Cumulative standings land in tramo 6d.
/// </summary>
public class League : BusinessEntity, ITenantOwned, IAuditable
{
    private League() { }

    public League(Guid tenantId, int tcgId, string name, DateOnly startsOn, DateOnly endsOn)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "La liga necesita un nombre.");
        DomainException.Require(endsOn >= startsOn, "La temporada no puede terminar antes de empezar.");

        TenantId = tenantId;
        TcgId = tcgId;
        Name = name.Trim();
        StartsOn = startsOn;
        EndsOn = endsOn;
        IsOpen = true;
    }

    public Guid TenantId { get; private set; }

    public int TcgId { get; private set; }

    public string Name { get; private set; } = null!;

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    /// <summary>A closed league no longer takes new tournaments.</summary>
    public bool IsOpen { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Renaming and moving the season. The game is not here on purpose: the tournaments already
    /// pointing at the league are of that game, and changing it would leave a Magic league adding
    /// up One Piece results.
    /// </summary>
    public void UpdateDetails(string name, DateOnly startsOn, DateOnly endsOn)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "La liga necesita un nombre.");
        DomainException.Require(endsOn >= startsOn, "La temporada no puede terminar antes de empezar.");

        Name = name.Trim();
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    public void Close()
    {
        DomainException.Require(IsOpen, "La liga ya estaba cerrada.");
        IsOpen = false;
    }

    public void Reopen()
    {
        DomainException.Require(!IsOpen, "La liga ya estaba abierta.");
        IsOpen = true;
    }
}
