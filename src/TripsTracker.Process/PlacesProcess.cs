using System.Diagnostics;
using System.Transactions;
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
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        var country = await _countries.GetByIsoAlpha2Async(dto.CountryIsoAlpha2, ct)
            ?? throw new NotFoundException("Country", dto.CountryIsoAlpha2);

        var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);

        // Cascade scoring: evaluate tiers BEFORE inserting so counts exclude the new place
        bool isFirstInCountry = !await _places.HasAnyInCountryAsync(country.Id, ct);
        bool isFirstInRegion = !await _places.HasAnyForCurrentUserInRegionAsync(country.Region, ct);
        bool isPioneerCity = !await _places.HasAnyGloballyInCityAsync(geocoded.City, country.Id, ct);
        bool isPioneerCountry = isFirstInCountry && !await _places.HasAnyGloballyInCountryAsync(country.Id, ct);
        bool isPioneerRegion = isFirstInRegion && !await _places.HasAnyGloballyInRegionAsync(country.Region, ct);

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, geocoded.StateName, dto.IsHome),
            ct);

        if (dto.IsHome)
            await _countries.SetAsHomeAsync(country.Id, ct);
        else
            await _countries.SetAsVisitedAsync(country.Id, ct);

        Debug.Assert(_userContext.UserId.HasValue);
        var userId = _userContext.UserId.Value;

        // City tier: 50 personal, 200 pioneer (pioneer = first globally in this specific city)
        var cityEvent = isPioneerCity ? "city_pioneer" : "city_added";
        var cityPoints = isPioneerCity ? 200 : 50;
        await _points.AwardAsync(userId, cityEvent, cityPoints, place.Id, "Place", ct);

        // Country tier: 500 personal, 2000 pioneer — stored as (place.Id, "Country") to enable reassignment
        if (isFirstInCountry)
        {
            var countryEvent = isPioneerCountry ? "country_pioneer" : "country_first";
            var countryPoints = isPioneerCountry ? 2000 : 500;
            await _points.AwardAsync(userId, countryEvent, countryPoints, place.Id, "Country", ct);
        }

        // Continent tier: 5000 personal, 20000 pioneer — stored as (place.Id, "Continent") to enable reassignment
        if (isFirstInRegion)
        {
            var regionEvent = isPioneerRegion ? "continent_pioneer" : "continent_first";
            var regionPoints = isPioneerRegion ? 20000 : 5000;
            await _points.AwardAsync(userId, regionEvent, regionPoints, place.Id, "Continent", ct);
        }

        scope.Complete();
        return place;
    }

    public async Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);
        var place = await _places.GetByIdAsync(id, ct);
        if (place is null) return null;
        var updated = await _places.UpdateAsync(id, dto, ct);
        if (updated is null) return null;
        if (dto.IsHome)
            await _countries.SetAsHomeAsync(place.CountryId, ct);
        scope.Complete();
        return updated;
    }

    public async Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

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

        Debug.Assert(_userContext.UserId.HasValue);
        var userId = _userContext.UserId.Value;

        // Always revoke city-tier points for this place
        await _points.RevokeAsync(userId, "city_", place.Id, "Place", ct);

        // Country tier: stored as (place.Id, "Country") — revoke or reassign
        if (!countryStillHasPlaces)
        {
            await _points.RevokeAsync(userId, "country_", place.Id, "Country", ct);
        }
        else
        {
            var survivingInCountry = await _places.GetFirstForCurrentUserInCountryAsync(place.CountryId, ct);
            if (survivingInCountry != null)
                await _points.ReassignAsync(userId, "country_", place.Id, "Country", survivingInCountry.Id, "Country", ct);
        }

        // Continent tier: stored as (place.Id, "Continent") — revoke or reassign
        var country = await _countries.GetByIdAsync(place.CountryId, ct);
        if (country != null)
        {
            var hasRemainingInRegion = await _places.HasAnyForCurrentUserInRegionAsync(country.Region, ct);
            if (!hasRemainingInRegion)
            {
                await _points.RevokeAsync(userId, "continent_", place.Id, "Continent", ct);
            }
            else
            {
                var survivingInRegion = await _places.GetFirstForCurrentUserInRegionAsync(country.Region, ct);
                if (survivingInRegion != null)
                    await _points.ReassignAsync(userId, "continent_", place.Id, "Continent", survivingInRegion.Id, "Continent", ct);
            }
        }

        scope.Complete();
        return new DeletePlaceResult(promptHomeCountry, promptHomeCountry ? place.CountryId : null, promptHomeCountry ? place.CountryName : null);
    }
}
