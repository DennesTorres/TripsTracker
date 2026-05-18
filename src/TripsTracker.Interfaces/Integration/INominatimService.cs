using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Integration;

public interface INominatimService
{
    /// <summary>
    /// Geocodes a city name using the Nominatim (OpenStreetMap) API.
    /// </summary>
    /// <param name="cityName">City name to search for.</param>
    /// <param name="countryIsoAlpha2Hint">ISO 3166-1 alpha-2 country code to narrow results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Geocoding result, or null if not found.</returns>
    Task<GeocodingResult?> GeocodeAsync(string cityName, string countryIsoAlpha2Hint, CancellationToken ct = default);

    /// <summary>
    /// Searches for city names matching a partial query string.
    /// Returns up to <paramref name="limit"/> results suitable for autocomplete suggestions.
    /// </summary>
    Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, int limit = 5, string countryCode = "", CancellationToken ct = default);
}
