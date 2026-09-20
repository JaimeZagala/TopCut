using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TopCut.Application.Abstractions;
using TopCut.Infrastructure.Identity;
using TopCut.Infrastructure.Persistence;
using TopCut.Infrastructure.Persistence.Interceptors;
using TopCut.Application.Tournaments;

namespace TopCut.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer. The presentation projects call this and nothing
/// else; they never reference EF Core types directly.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddTopCutInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingletonTimeProvider();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<AuditingInterceptor>();
        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<TopCutDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // The Azure SQL free tier is serverless and pauses when idle, so the first query
                // after a quiet spell can time out while the database wakes up.
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null);
            });

            options.AddInterceptors(
                provider.GetRequiredService<AuditingInterceptor>(),
                provider.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        // One DbContext per operation instead of one per request (ADR-0023). The scoped instance
        // stays registered because Identity's stores expect to be handed one.
        services.AddScoped<IDbContextFactory<TopCutDbContext>, TopCutDbContextFactory>();

        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ITournamentQueries, TournamentQueries>();
        services.AddScoped<ITcgCatalog, TcgCatalog>();
        services.AddScoped<TournamentService>();
        services.AddScoped<TournamentSetupService>();
        services.AddScoped<ParticipantService>();

        services.AddTopCutIdentity();

        return services;
    }

    /// <summary>
    /// ASP.NET Core Identity over the same DbContext. Only the core services and the stores live
    /// here; which authentication schemes are wired up (cookie, bearer, Google) is a decision of
    /// the host and belongs to the presentation layer.
    /// </summary>
    private static void AddTopCutIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<TopCutUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;

                // Sensible for a local store: long enough to matter, short enough that nobody is
                // locked out of a tournament they are standing in the queue for.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TopCutDbContext>()
            .AddClaimsPrincipalFactory<TopCutUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IStoreOnboarding, StoreOnboarding>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<IExternalSignInService, ExternalSignInService>();
        services.AddScoped<IStoreDirectory, StoreDirectory>();
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
