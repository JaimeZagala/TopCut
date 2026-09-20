using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TopCut.Application.Abstractions;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without booting the web application. Only ever used by the
/// migration tooling at design time; nothing in the running application goes through it.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TopCutDbContext>
{
    /// <summary>
    /// The SQL Server that <c>compose.yaml</c> at the repository root brings up (ADR-0015).
    /// Override it with the <c>TOPCUT_CONNECTION</c> environment variable to point the tooling
    /// somewhere else.
    /// </summary>
    private const string DesignTimeConnection =
        "Server=localhost,11433;Database=TopCut;User Id=sa;Password=TopCut-Local-2026;TrustServerCertificate=True";

    public TopCutDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TOPCUT_CONNECTION") ?? DesignTimeConnection;

        var options = new DbContextOptionsBuilder<TopCutDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new TopCutDbContext(options, new NoTenantContext());
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
