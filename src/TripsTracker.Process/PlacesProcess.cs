using System.Transactions;
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

        if (string.Equals(dto.CityName.Trim(), country.Name, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                $"'{dto.CityName}' is a country name, not a city. Please enter a specific city within {country.Name}.",
                "CITY_IS_COUNTRY");

        var geocoded = await _geocoding.GeocodeAsync(dto.CityName, country, ct);

        var place = await _places.CreateAsync(
            new CreatePlaceDto(geocoded.Lon, geocoded.Lat, country.Id, geocoded.City, geocoded.StateAbbr, geocoded.StateName, dto.IsHome),
            ct);

        if (dto.IsHome)
            await _countries.SetHomeAsync(country.Id, true, ct);
        else
            await _countries.SetVisitedAsync(country.Id, true, ct);

        return place;
    }

    public async Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var place = await _places.GetByIdAsync(id, ct);
        if (place is null) return null;

        var updated = await _places.UpdateAsync(id, dto, ct);
        if (updated is null) return null;

        if (dto.IsHome)
            await _countries.SetHomeAsync(place.CountryId, true, ct);

        scope.Complete();
        return updated;
    }

    public async Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var place = await _places.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await _places.DeleteAsync(placeId, ct);

        var hasRemainingPlaces = await _places.HasAnyInCountryAsync(place.CountryId, ct);
        if (!hasRemainingPlaces)
            await _countries.SetVisitedAsync(place.CountryId, false, ct);

        var promptHomeCountry = false;
        if (place.IsHome)
        {
            var hasRemainingHome = await _places.HasHomeInCountryAsync(place.CountryId, ct);
            if (!hasRemainingHome)
            {
                await _countries.SetHomeAsync(place.CountryId, false, ct);
                promptHomeCountry = true;
            }
        }

        scope.Complete();
        return new DeletePlaceResult(promptHomeCountry, promptHomeCountry ? place.CountryId : null, promptHomeCountry ? place.CountryName : null);
    }
}
