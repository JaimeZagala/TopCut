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
    private const string DesignTimeConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=TopCut;Trusted_Connection=True;MultipleActiveResultSets=true";

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
