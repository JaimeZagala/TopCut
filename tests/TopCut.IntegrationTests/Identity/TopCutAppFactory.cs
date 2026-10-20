using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TopCut.IntegrationTests.Persistence;

namespace TopCut.IntegrationTests.Identity;

/// <summary>
/// Boots the real application — the same <c>Program</c> that runs in production, migrations,
/// seeding and all — against the throwaway PostgreSQL. Nothing about authentication is stubbed:
/// these tests go through actual HTTP with real cookies and real bearer tokens.
/// </summary>
public sealed class TopCutAppFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    public const string SuperadminEmail = "superadmin@topcut.test";
    public const string SuperadminPassword = "Superadmin-1234";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:TopCut"] = postgres.ConnectionString,
            ["Superadmin:Email"] = SuperadminEmail,
            ["Superadmin:Password"] = SuperadminPassword
        }));

        return base.CreateHost(builder);
    }
}
