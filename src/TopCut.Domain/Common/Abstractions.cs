namespace TopCut.Domain.Common;

/// <summary>
/// Marks an entity as belonging to one store. The EF Core global query filter keys off this
/// interface, so no use case ever has to filter by tenant by hand.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}

/// <summary>
/// Lets infrastructure collect and clear domain events without knowing the entity's key type.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>Creation and last-modification timestamps, filled in by an EF Core interceptor.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Optimistic concurrency. Implemented by entities where two simultaneous actions can collide:
/// reporting a result, taking the last seat, promoting from the waitlist.
/// <para>
/// The database bumps it on every write to the row; nobody sets it by hand. On PostgreSQL it is
/// the system column <c>xmin</c>, which is why it is a 32-bit number (ADR-0039).
/// </para>
/// </summary>
public interface IHasRowVersion
{
    uint RowVersion { get; set; }
}
