using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Business;

public class GeocodingBusiness : IGeocodingBusiness
{
    private readonly INominatimService _nominatim;

    public GeocodingBusiness(INominatimService nominatim)
    {
        _nominatim = nominatim;
    }

    public Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, CancellationToken ct = default)
        => _nominatim.SuggestCitiesAsync(query, ct: ct);

    public async Task<GeocodingResult> GeocodeAsync(string cityName, CountryDto country, CancellationToken ct = default)
    {
        var result = await _nominatim.GeocodeAsync(cityName, country.IsoAlpha2, ct)
            ?? throw new BusinessRuleException(
                $"No city matching '{cityName}' found in {country.Name}. Try a different city name.",
                "GEOCODING_FAILED");

        var inputWords = cityName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cityMatches = inputWords.Any(w => result.City.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (!cityMatches)
            throw new GeocodingMismatchException(cityName, result.City, country.Name);

        return result;
    }
}
