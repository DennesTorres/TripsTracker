using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IGeocodingBusiness
{
    /// <summary>
    /// Geocodes a city name using Photon as primary (for coordinates and city name) and
    /// Nominatim as secondary (for StateAbbr only). Throws BusinessRuleException if the
    /// city is not found.
    /// </summary>
    Task<GeocodingResult> GeocodeAsync(string cityName, CountryDto country, CancellationToken ct = default);

    /// <summary>
    /// Returns up to 5 city suggestions for an autocomplete input.
    /// Query must be at least 3 characters; returns empty list otherwise.
    /// </summary>
    Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, string countryCode = "", CancellationToken ct = default);
}
