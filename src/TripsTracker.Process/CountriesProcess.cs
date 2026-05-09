using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class CountriesProcess : ICountriesProcess
{
    private readonly ICountryBusiness _countries;
    private readonly IGeoBoundariesService _geoBoundaries;

    public CountriesProcess(ICountryBusiness countries, IGeoBoundariesService geoBoundaries)
    {
        _countries = countries;
        _geoBoundaries = geoBoundaries;
    }

    public async Task<string?> GetBordersAsync(int countryId, CancellationToken ct = default)
    {
        var isoAlpha3 = await _countries.GetIsoAlpha3Async(countryId, ct);
        if (string.IsNullOrEmpty(isoAlpha3)) return null;
        return await _geoBoundaries.GetBordersAsync(isoAlpha3, ct);
    }
}
