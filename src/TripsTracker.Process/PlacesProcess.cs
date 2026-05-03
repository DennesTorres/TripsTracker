using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class PlacesProcess : IPlacesProcess
{
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;
    private readonly IGeocodingBusiness _geocoding;
    private readonly IPointsBusiness _points;
    private readonly IUserContext _userContext;

    public PlacesProcess(IPlaceBusiness places, ICountryBusiness countries, IGeocodingBusiness geocoding,
        IPointsBusiness points, IUserContext userContext)
    {
        _places = places;
        _countries = countries;
        _geocoding = geocoding;
        _points = points;
        _userContext = userContext;
    }

    public async Task<PlaceDto> AddAsync(AddPlaceDto dto, CancellationToken ct = default)
    {
        var country = await _countries.GetByIsoAlpha2Async(dto.CountryIsoAlpha2, ct)
            ?? throw new NotFoundException("Country", dto.CountryIsoAlpha2);

        var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);

        // Cascade scoring: evaluate tiers BEFORE inserting so counts exclude the new place
        bool isFirstInCountry = !await _places.HasAnyInCountryAsync(country.Id, ct);
        bool isFirstInRegion = !await _places.HasAnyForCurrentUserInRegionAsync(country.Region, ct);
        bool isPioneerCountry = isFirstInCountry && !await _places.HasAnyGloballyInCountryAsync(country.Id, ct);
        bool isPioneerRegion = isFirstInRegion && !await _places.HasAnyGloballyInRegionAsync(country.Region, ct);

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, geocoded.StateName, dto.IsHome),
            ct);

        if (dto.IsHome)
            await _countries.SetHomeAsync(country.Id, true, ct);
        else
            await _countries.SetVisitedAsync(country.Id, true, ct);

        if (_userContext.UserId.HasValue)
        {
            var userId = _userContext.UserId.Value;
            // City tier: 50 personal, 200 pioneer
            var cityEvent = isPioneerCountry || isPioneerRegion ? "city_pioneer" : "city_added";
            var cityPoints = isPioneerCountry || isPioneerRegion ? 200 : 50;
            await _points.AwardAsync(userId, cityEvent, cityPoints, place.Id, "Place", ct);

            // Country tier: 500 personal, 2000 pioneer
            if (isFirstInCountry)
            {
                var countryEvent = isPioneerCountry ? "country_pioneer" : "country_first";
                var countryPoints = isPioneerCountry ? 2000 : 500;
                await _points.AwardAsync(userId, countryEvent, countryPoints, country.Id, "Country", ct);
            }

            // Continent tier: 5000 personal, 20000 pioneer
            if (isFirstInRegion)
            {
                var regionEvent = isPioneerRegion ? "continent_pioneer" : "continent_first";
                var regionPoints = isPioneerRegion ? 20000 : 5000;
                await _points.AwardAsync(userId, regionEvent, regionPoints, place.Id, "Place", ct);
            }
        }

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
