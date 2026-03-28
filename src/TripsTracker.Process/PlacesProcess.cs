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

        double lat, lon;
        string city;
        string? stateAbbr, stateName;

        if (dto.Lat.HasValue && dto.Lon.HasValue)
        {
            // Coordinates pre-resolved by Photon autocomplete — skip Nominatim re-geocoding.
            // This preserves the user-selected city name (e.g. "Itacuruça") rather than
            // storing Nominatim's potentially different canonical spelling ("Itacurussa").
            lat = dto.Lat.Value;
            lon = dto.Lon.Value;
            city = dto.CityName;
            stateAbbr = dto.StateAbbr;
            stateName = dto.StateName;
        }
        else
        {
            var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);
            lat = geocoded.Lat;
            lon = geocoded.Lon;
            city = geocoded.City;
            stateAbbr = geocoded.StateAbbr;
            stateName = geocoded.StateName;
        }

        var place = await _places.CreateAsync(
            new CreatePlaceDto(lon, lat, country.Id, city, stateAbbr, stateName, dto.IsHome),
            ct);

        if (dto.IsHome)
            await _countries.SetHomeAsync(country.Id, true, ct);
        else if (!country.IsVisited)
            await _countries.SetVisitedAsync(country.Id, true, ct);

        return place;
    }

    public async Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default)
    {
        var place = await _places.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await _places.DeleteAsync(placeId, ct);

        var hasRemainingPlaces = await _places.HasAnyInCountryAsync(place.CountryId, ct);
        if (!hasRemainingPlaces)
            await _countries.SetVisitedAsync(place.CountryId, false, ct);

        if (place.IsHome)
        {
            var hasRemainingHome = await _places.HasHomeInCountryAsync(place.CountryId, ct);
            if (!hasRemainingHome)
                await _countries.SetHomeAsync(place.CountryId, false, ct);
        }

        return new DeletePlaceResult(false, null, null);
    }
}
