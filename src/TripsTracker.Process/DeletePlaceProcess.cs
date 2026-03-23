using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class DeletePlaceProcess : IDeletePlaceProcess
{
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;

    public DeletePlaceProcess(IPlaceBusiness places, ICountryBusiness countries)
    {
        _places = places;
        _countries = countries;
    }

    public async Task<DeletePlaceResult> ExecuteAsync(int placeId, CancellationToken ct = default)
    {
        var place = await _places.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        await _places.DeleteAsync(placeId, ct);

        var hasRemainingPlaces = await _places.HasAnyInCountryAsync(place.CountryId, ct);
        if (!hasRemainingPlaces)
            await _countries.SetVisitedAsync(place.CountryId, false, ct);

        return new DeletePlaceResult(
            PromptHomeCountry: place.IsHome,
            CountryId: place.IsHome ? place.CountryId : null,
            CountryName: place.IsHome ? place.CountryName : null);
    }
}
