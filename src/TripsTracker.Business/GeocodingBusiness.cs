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

    public Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, string countryCode = "", CancellationToken ct = default)
        => _nominatim.SuggestCitiesAsync(query, countryCode: countryCode, ct: ct);

    public async Task<GeocodingResult> GeocodeAsync(string cityName, CountryDto country, CancellationToken ct = default)
    {
        var result = await _nominatim.GeocodeAsync(cityName, country.IsoAlpha2, ct);
        if (result is null)
            throw new BusinessRuleException(
                $"No city matching '{cityName}' found in {country.Name}. Try a different city name.",
                "GEOCODING_FAILED");
        return result;
    }
}
