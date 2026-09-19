namespace TopCut.Domain.Common;

/// <summary>
/// Common root for domain entities. Collects domain events that infrastructure publishes when
/// changes are saved (see docs/arquitectura.md).
/// </summary>
public abstract class Entity<TId> where TId : struct
{
    private readonly List<DomainEvent> _domainEvents = [];

    public TId Id { get; protected set; }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Business entity: keyed by a v7 <see cref="Guid"/>, time-ordered, safe to expose in public URLs
/// without leaking how much data the platform holds (ADR-0006).
/// </summary>
public abstract class BusinessEntity : Entity<Guid>
{
    protected BusinessEntity() => Id = Guid.CreateVersion7();
}

/// <summary>
/// Catalog entity: auto-incrementing <see cref="int"/> key. For small, stable tables that never
/// appear in public URLs (ADR-0006).
/// </summary>
public abstract class CatalogEntity : Entity<int>;
