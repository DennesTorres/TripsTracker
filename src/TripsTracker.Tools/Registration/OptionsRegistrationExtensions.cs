using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tools.Registration;

/// <summary>
/// Registers strongly-typed IOptions configuration classes using the IOptions pattern.
/// </summary>
public static class OptionsRegistrationExtensions
{
    /// <summary>
    /// Registers all application configuration options with validation on startup.
    /// </summary>
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ApplicationInsightsOptions>()
            .Bind(configuration.GetSection(ApplicationInsightsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
