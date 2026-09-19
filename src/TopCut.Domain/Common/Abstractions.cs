namespace TopCut.Domain.Common;

/// <summary>
/// Marks an entity as belonging to one store. The EF Core global query filter keys off this
/// interface, so no use case ever has to filter by tenant by hand.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
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
/// </summary>
public interface IHasRowVersion
{
    byte[]? RowVersion { get; set; }
}
