using Microsoft.Extensions.Logging;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class CountriesProcess : ICountriesProcess
{
    private readonly ICountryBusiness _countries;
    private readonly IGeoBoundariesService _geoBoundaries;
    private readonly IBorderCacheService _borderCache;
    private readonly ILogger<CountriesProcess> _logger;

    public CountriesProcess(ICountryBusiness countries, IGeoBoundariesService geoBoundaries, IBorderCacheService borderCache, ILogger<CountriesProcess> logger)
    {
        _countries = countries;
        _geoBoundaries = geoBoundaries;
        _borderCache = borderCache;
        _logger = logger;
    }

    public async Task<string?> GetBordersAsync(int countryId, CancellationToken ct = default)
    {
        var isoAlpha3 = await _countries.GetIsoAlpha3Async(countryId, ct);
        if (string.IsNullOrEmpty(isoAlpha3)) return null;

        var cached = await _borderCache.GetAsync(isoAlpha3, ct);
        if (cached != null) return cached;

        var json = await _geoBoundaries.GetBordersAsync(isoAlpha3, ct);
        if (json != null)
        {
            try
            {
                await _borderCache.SetAsync(isoAlpha3, json, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Border cache write failed for {IsoAlpha3} — returning GeoJSON without caching", isoAlpha3);
            }
        }

        return json;
    }
}
