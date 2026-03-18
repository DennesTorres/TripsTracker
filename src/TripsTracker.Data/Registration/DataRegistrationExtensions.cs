using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Data.Registration;

/// <summary>
/// Registers the application DbContext with SQL Server and Azure authentication.
/// Call from the root project after <c>AddApplicationOptions</c>.
/// </summary>
public static class DataRegistrationExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a Scoped DbContext using the
    /// connection string from <see cref="DatabaseOptions"/>.
    ///
    /// The connection string should include <c>Authentication=Active Directory Default</c>
    /// so that <c>DefaultAzureCredential</c> is used automatically:
    /// local dev uses <c>az login</c>, Azure uses Managed Identity.
    /// </summary>
    public static IServiceCollection AddDatabaseContext<TContext>(
        this IServiceCollection services,
        DatabaseOptions options)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(opt =>
            opt.UseSqlServer(options.ConnectionString),
            ServiceLifetime.Scoped);

        return services;
    }
}
