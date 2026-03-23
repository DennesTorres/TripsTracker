using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class AddPlaceProcess : IAddPlaceProcess
{
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;
    private readonly IGeocodingBusiness _geocoding;

    public AddPlaceProcess(IPlaceBusiness places, ICountryBusiness countries, IGeocodingBusiness geocoding)
    {
        _places = places;
        _countries = countries;
        _geocoding = geocoding;
    }

    public async Task<PlaceDto> ExecuteAsync(AddPlaceDto dto, CancellationToken ct = default)
    {
        var country = await _countries.GetByIsoAlpha2Async(dto.CountryIsoAlpha2, ct)
            ?? throw new NotFoundException("Country", dto.CountryIsoAlpha2);

        var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, dto.IsHome),
            ct);

        if (!country.IsVisited && !country.IsHome)
            await _countries.SetVisitedAsync(country.Id, true, ct);

        return place;
    }
}
