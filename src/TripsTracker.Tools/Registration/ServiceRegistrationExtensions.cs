using Microsoft.Extensions.DependencyInjection;

namespace TripsTracker.Tools.Registration;

/// <summary>
/// Provides auto-registration of application services using Scrutor assembly scanning.
/// Registers all custom classes that implement an interface, excluding framework types.
/// </summary>
public static class ServiceRegistrationExtensions
{
    private static readonly string[] _applicationAssemblyPrefixes =
    [
        "TripsTracker."
    ];

    /// <summary>
    /// Scans the specified assemblies and registers all application classes
    /// that implement an interface, using the specified lifetime.
    /// Only classes whose assembly name starts with the application prefix are registered.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        params string[] assemblyPrefixes)
    {
        var prefixes = assemblyPrefixes.Length > 0 ? assemblyPrefixes : _applicationAssemblyPrefixes;

        services.Scan(scan => scan
            .FromApplicationDependencies(a => prefixes.Any(p => a.GetName().Name?.StartsWith(p) == true))
            .AddClasses(classes => classes
                .Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition))
            .UsingRegistrationStrategy(Scrutor.RegistrationStrategy.Skip)
            .AsImplementedInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    /// <summary>
    /// Registers application services with scoped lifetime (default for DbContext-dependent services).
    /// </summary>
    public static IServiceCollection AddScopedApplicationServices(
        this IServiceCollection services,
        params string[] assemblyPrefixes)
        => services.AddApplicationServices(ServiceLifetime.Scoped, assemblyPrefixes);

    /// <summary>
    /// Registers application services with transient lifetime.
    /// </summary>
    public static IServiceCollection AddTransientApplicationServices(
        this IServiceCollection services,
        params string[] assemblyPrefixes)
        => services.AddApplicationServices(ServiceLifetime.Transient, assemblyPrefixes);
}
