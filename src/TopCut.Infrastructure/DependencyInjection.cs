using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TopCut.Application.Abstractions;
using TopCut.Infrastructure.Persistence;
using TopCut.Infrastructure.Persistence.Interceptors;

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

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
