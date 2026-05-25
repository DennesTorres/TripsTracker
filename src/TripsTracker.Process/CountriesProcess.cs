using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class CountriesProcess : ICountriesProcess
{
    private readonly ICountryBusiness _countries;
    private readonly IGeoBoundariesService _geoBoundaries;
    private readonly IBorderCacheService _borderCache;

    public CountriesProcess(ICountryBusiness countries, IGeoBoundariesService geoBoundaries, IBorderCacheService borderCache)
    {
        _countries = countries;
        _geoBoundaries = geoBoundaries;
        _borderCache = borderCache;
    }

    public async Task<string?> GetBordersAsync(int countryId, CancellationToken ct = default)
    {
        var isoAlpha3 = await _countries.GetIsoAlpha3Async(countryId, ct);
        if (string.IsNullOrEmpty(isoAlpha3)) return null;

        var cached = await _borderCache.GetAsync(isoAlpha3, ct);
        if (cached != null) return cached;

        var json = await _geoBoundaries.GetBordersAsync(isoAlpha3, ct);
        if (json != null)
            await _borderCache.SetAsync(isoAlpha3, json, ct);

        return json;
    }
}
