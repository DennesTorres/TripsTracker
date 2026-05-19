using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Integration.Registration;

public static class IntegrationRegistrationExtensions
{
    /// <summary>
    /// Registers typed HTTP clients for all integration services.
    /// Must be called before Scrutor's AddScopedApplicationServices so TryAdd skips these.
    /// </summary>
    public static IServiceCollection AddIntegrationHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<INominatimService, NominatimGeocodingService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<NominatimOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
            client.DefaultRequestHeaders.Add("Accept-Language", "en");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<IGeoBoundariesService, GeoBoundariesService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
