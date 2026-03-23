using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IGeocodingBusiness
{
    /// <summary>
    /// Geocodes a city name using Nominatim, validates the result matches the requested city,
    /// and returns the geocoding result. Throws BusinessRuleException if geocoding fails or
    /// returns a city that doesn't match the requested city name.
    /// </summary>
    Task<GeocodingResult> GeocodeAsync(string cityName, CountryDto country, CancellationToken ct = default);

    /// <summary>
    /// Returns up to 5 city suggestions for an autocomplete input.
    /// Query must be at least 3 characters; returns empty list otherwise.
    /// </summary>
    Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, CancellationToken ct = default);
}
