using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IPlacesProcess
{
    /// <summary>
    /// Geocodes <paramref name="dto"/>, creates the place, and marks the country as visited.
    /// </summary>
    Task<PlaceDto> AddAsync(AddPlaceDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates a place. When <see cref="UpdatePlaceDto.IsHome"/> is true, also syncs
    /// <see cref="UserCountry.IsHome"/> so the profile home-country dropdown reflects the change.
    /// </summary>
    Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default);

    /// <summary>
    /// Sets a place as the user's home: clears IsHome on all other places, marks the target,
    /// and syncs UserCountry.IsHome. Runs within a TransactionScope.
    /// </summary>
    Task SetHomeAsync(int placeId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a place and manages related country flags:
    /// - Unsets country IsVisited if this was the last place in the country.
    /// - Returns PromptHomeCountry=true if the deleted place was the user's home city,
    ///   so the frontend can ask whether the country is still the home country.
    /// </summary>
    Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default);
}
