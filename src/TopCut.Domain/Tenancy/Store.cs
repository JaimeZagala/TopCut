using TopCut.Domain.Common;

namespace TopCut.Domain.Tenancy;

/// <summary>
/// Anyone can sign up, but a store cannot publish tournaments until a Superadmin approves it. This
/// is the infrastructure cost control described in docs/vision.md.
/// </summary>
public enum StoreStatus
{
    PendingApproval = 1,
    Approved = 2,
    Revoked = 3
}

/// <summary>
/// An organising store. It is also the <b>tenant</b>: there is no separate entity, a store
/// <i>is</i> a tenant (see docs/modelo-datos.md).
/// </summary>
public class Store : BusinessEntity, IAuditable
{
    private Store() { }

    public Store(string name, Address address, bool isTestStore = false)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "La tienda necesita un nombre.");

        Name = name.Trim();
        Address = address;
        IsTestStore = isTestStore;
        Status = StoreStatus.PendingApproval;
    }

    public string Name { get; private set; } = null!;

    public Address Address { get; private set; } = null!;

    public StoreStatus Status { get; private set; }

    /// <summary>
    /// Test tenant: excluded from public search. Stands in for a full staging environment, which
    /// the Azure SQL free tier cannot fund.
    /// </summary>
    public bool IsTestStore { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Allowed to create and publish tournaments.</summary>
    public bool CanOrganize => Status == StoreStatus.Approved;

    public void Approve()
    {
        DomainException.Require(Status != StoreStatus.Approved, "La tienda ya estaba aprobada.");
        Status = StoreStatus.Approved;
    }

    public void RevokeApproval()
    {
        DomainException.Require(Status == StoreStatus.Approved, "Solo se puede revocar una tienda aprobada.");
        Status = StoreStatus.Revoked;
    }

    public void UpdateDetails(string name, Address address)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(name), "La tienda necesita un nombre.");
        Name = name.Trim();
        Address = address;
    }
}
