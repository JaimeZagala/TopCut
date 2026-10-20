using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using TopCut.Infrastructure.Identity;

namespace TopCut.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date and seeds it at startup. Every step is idempotent, so it is safe
/// to run on every boot.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// How long to keep knocking before giving up. Enough for a Docker container that is still
    /// starting, or for Neon to resume a compute suspended while idle.
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
    /// still waking up, EF concludes the database is missing and tries to create it, which then
    /// fails because it was there all along. It bites on Neon's free tier, which suspends the
    /// compute when idle, and on a local container that has not finished starting.
    /// </para>
    /// So this asks only "is the engine up?", never "does our database exist?": a server that
    /// answers that the database is missing (<c>3D000</c>) has answered, and creating it is EF's job.
    /// It does not go through the <c>postgres</c> maintenance database because a hosted role is not
    /// guaranteed to be allowed in there.
    /// </summary>
    private static async Task WaitForServerAsync(
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var withShortTimeout = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 15
        }.ConnectionString;

        var deadline = DateTimeOffset.UtcNow + WakeUpBudget;
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                await using var connection = new NpgsqlConnection(withShortTimeout);
                await connection.OpenAsync(cancellationToken);

                if (attempt > 1)
                {
                    logger.LogInformation("El servidor de base de datos respondio al intento {Intento}.", attempt);
                }

                return;
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InvalidCatalogName)
            {
                return;
            }
            catch (Exception error) when (error is NpgsqlException or TimeoutException && DateTimeOffset.UtcNow < deadline)
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
