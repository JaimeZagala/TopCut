using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TopCut.Application.Abstractions;
using TopCut.Domain.Common;
using TopCut.Infrastructure.Persistence;
using TopCut.Infrastructure.Persistence.Interceptors;

namespace TopCut.IntegrationTests.Persistence;

/// <summary>
/// Spins up a real, disposable SQL Server in Docker for the whole test class collection and applies
/// the EF Core migrations to it. Deliberately not the InMemory provider: the point of these tests is
/// to check the schema against the same engine production runs, including the constraints, filtered
/// indexes and rowversion semantics InMemory does not have (see docs/arquitectura.md).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Pinned to the SQL Server major version Azure SQL is built on, so the constraints these
    // tests exercise are the ones production will enforce.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    /// <summary>
    /// Points the suite at an existing SQL Server instead of starting a container. Useful when
    /// Docker is not available locally, and for a CI runner that already provides SQL Server as a
    /// service container. Unset, which is the normal case, the suite starts its own container.
    /// </summary>
    private static readonly string? ExternalConnection =
        Environment.GetEnvironmentVariable("TOPCUT_TEST_SQL");

    private readonly MsSqlContainer? _container =
        ExternalConnection is null ? new MsSqlBuilder(Image).Build() : null;

    public string ConnectionString => ExternalConnection ?? _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }

        await using var db = CreateContext();

        // An external server is reused between runs, so start from a clean schema every time.
        if (ExternalConnection is not null)
        {
            await db.Database.EnsureDeletedAsync();
        }

        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// A context scoped to one store, exactly as a web request would build it. Pass
    /// <c>null</c> to get a context with no tenant in scope.
    /// </summary>
    public TopCutDbContext CreateContext(
        Guid? tenantId = null,
        IDomainEventDispatcher? dispatcher = null,
        TimeProvider? timeProvider = null)
    {
        var builder = new DbContextOptionsBuilder<TopCutDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(new AuditingInterceptor(timeProvider ?? TimeProvider.System));

        if (dispatcher is not null)
        {
            builder.AddInterceptors(new DomainEventDispatchInterceptor(dispatcher));
        }

        return new TopCutDbContext(builder.Options, new FixedTenantContext(tenantId));
    }

    /// <summary>
    /// Hands out a fresh context per call, scoped to one store, the way the application does
    /// (ADR-0023). Services that own their unit of work take this rather than a single context.
    /// </summary>
    public IDbContextFactory<TopCutDbContext> CreateContextFactory(
        Guid? tenantId = null,
        IDomainEventDispatcher? dispatcher = null,
        TimeProvider? timeProvider = null) =>
        new FixtureContextFactory(() => CreateContext(tenantId, dispatcher, timeProvider));

    private sealed class FixtureContextFactory(Func<TopCutDbContext> create) : IDbContextFactory<TopCutDbContext>
    {
        public TopCutDbContext CreateDbContext() => create();
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }
}

/// <summary>Shares one container across every test class in the collection; starting SQL Server is slow.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sqlserver";
}

/// <summary>Records what was dispatched, so tests can assert on domain events without a real handler.</summary>
public sealed class RecordingDispatcher : IDomainEventDispatcher
{
    public List<DomainEvent> Dispatched { get; } = [];

    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        Dispatched.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}
