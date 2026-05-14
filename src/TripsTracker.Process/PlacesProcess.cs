using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class PlacesProcess : IPlacesProcess
{
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;
    private readonly IGeocodingBusiness _geocoding;

    public PlacesProcess(IPlaceBusiness places, ICountryBusiness countries, IGeocodingBusiness geocoding)
    {
        _places = places;
        _countries = countries;
        _geocoding = geocoding;
    }

    public async Task<PlaceDto> AddAsync(AddPlaceDto dto, CancellationToken ct = default)
    {
        var country = await _countries.GetByIsoAlpha2Async(dto.CountryIsoAlpha2, ct)
            ?? throw new NotFoundException("Country", dto.CountryIsoAlpha2);

        var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, geocoded.StateName, dto.IsHome),
            ct);

        if (dto.IsHome)
            await _countries.SetAsHomeAsync(country.Id, ct);
        else
            await _countries.SetAsVisitedAsync(country.Id, ct);

        return place;
    }

    public async Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default)
    {
        var place = await _places.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await _places.DeleteAsync(placeId, ct);

        var countryStillHasPlaces = await _places.HasAnyInCountryAsync(place.CountryId, ct);
        if (!countryStillHasPlaces)
            await _countries.UnsetVisitedAsync(place.CountryId, ct);

        var promptHomeCountry = false;
        if (place.IsHome)
        {
            var countryStillHasHomePlace = await _places.HasHomeInCountryAsync(place.CountryId, ct);
            if (!countryStillHasHomePlace)
            {
                await _countries.UnsetHomeAsync(place.CountryId, ct);
                promptHomeCountry = true;
            }
        }

        return new DeletePlaceResult(promptHomeCountry, promptHomeCountry ? place.CountryId : null, promptHomeCountry ? place.CountryName : null);
    }
}
