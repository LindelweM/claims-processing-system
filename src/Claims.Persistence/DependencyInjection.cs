using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Persistence;

/// <summary>
/// Registers the claims store with the application's service container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="ClaimsDbContext"/> and <see cref="IClaimRepository"/>.
    /// </summary>
    /// <param name="services">Container to register into.</param>
    /// <param name="connectionString">
    /// Connection string for the claims database. To authenticate with a managed identity,
    /// include <c>Authentication=Active Directory Default</c> rather than a password.
    /// </param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddClaimsPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ClaimsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Azure SQL drops idle connections and throttles under load, so transient
                // failures are expected rather than exceptional.
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(ClaimsDbContext).Assembly.FullName);
            }));

        services.AddScoped<IClaimRepository, ClaimsRepository>();

        return services;
    }
}
