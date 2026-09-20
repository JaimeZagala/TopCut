using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TopCut.Infrastructure.Identity;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date and seeds it at startup. Every step is idempotent, so it is safe
/// to run on every boot.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// How long to keep knocking before giving up. Enough for LocalDB to auto-start or for Azure
    /// SQL serverless to resume from its idle pause.
    /// </summary>
    private static readonly TimeSpan WakeUpBudget = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan BetweenAttempts = TimeSpan.FromSeconds(3);

    public static async Task InitializeAsync(
        IServiceProvider services,
        string connectionString,
        string? superadminEmail,
        string? superadminPassword,
        CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        await WaitForServerAsync(connectionString, logger, cancellationToken);

        var db = services.GetRequiredService<TopCutDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await CatalogSeeder.SeedAsync(db, cancellationToken);

        await IdentitySeeder.SeedRolesAsync(services.GetRequiredService<RoleManager<IdentityRole>>());

        var superadmin = await IdentitySeeder.SeedSuperadminAsync(
            services.GetRequiredService<UserManager<TopCutUser>>(),
            superadminEmail,
            superadminPassword);

        if (superadmin is { Succeeded: false })
        {
            // Almost always the password failing the policy. Silence here would leave the platform
            // with nobody able to approve the first store, and no clue why.
            logger.LogError(
                "No se pudo crear el Superadmin de arranque: {Errores}",
                string.Join(" ", superadmin.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Waits for the server to answer <b>before</b> touching migrations.
    /// <para>
    /// Without this, a paused server loses the race: EF's existence check fails while the engine is
    /// still waking up, EF concludes the database is missing and issues CREATE DATABASE, which then
    /// fails because the files were there all along. It bites on LocalDB, which shuts itself down
    /// when idle, and on Azure SQL serverless, which does the same on the free tier.
    /// </para>
    /// The connection targets <c>master</c>, which always exists, so this is purely "is the engine
    /// up?" and never "does our database exist?".
    /// </summary>
    private static async Task WaitForServerAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var toMaster = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 15
        }.ConnectionString;

        var deadline = DateTimeOffset.UtcNow + WakeUpBudget;
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                await using var connection = new SqlConnection(toMaster);
                await connection.OpenAsync(cancellationToken);

                if (attempt > 1)
                {
                    logger.LogInformation("El servidor de base de datos respondio al intento {Intento}.", attempt);
                }

                return;
            }
            catch (SqlException error) when (DateTimeOffset.UtcNow < deadline)
            {
                logger.LogInformation(
                    "Esperando a que el servidor de base de datos arranque (intento {Intento}): {Mensaje}",
                    attempt,
                    error.Message);

                await Task.Delay(BetweenAttempts, cancellationToken);
            }
        }
    }
}
