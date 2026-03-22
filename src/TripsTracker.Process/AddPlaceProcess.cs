using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class AddPlaceProcess : IAddPlaceProcess
{
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;
    private readonly INominatimService _nominatim;

    public AddPlaceProcess(IPlaceBusiness places, ICountryBusiness countries, INominatimService nominatim)
    {
        _places = places;
        _countries = countries;
        _nominatim = nominatim;
    }

    public async Task<PlaceDto> ExecuteAsync(AddPlaceDto dto, CancellationToken ct = default)
    {
        var country = await _countries.GetByIsoAlpha2Async(dto.CountryIsoAlpha2, ct)
            ?? throw new NotFoundException("Country", dto.CountryIsoAlpha2);

        var geocoded = await _nominatim.GeocodeAsync(dto.CityName, dto.CountryIsoAlpha2, ct)
            ?? throw new BusinessRuleException(
                $"No city matching '{dto.CityName}' found in {country.Name}. Try a different city name.",
                "GEOCODING_FAILED");

        var inputWords = dto.CityName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cityMatches = inputWords.Any(w => geocoded.City.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (!cityMatches)
            throw new BusinessRuleException(
                $"No city matching '{dto.CityName}' found in {country.Name}. Try a different city name.",
                "GEOCODING_MISMATCH");

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, dto.IsHome),
            ct);

        if (!country.IsVisited && !country.IsHome)
            await _countries.SetVisitedAsync(country.Id, true, ct);

        return place;
    }
}
