using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IPlacesProcess
{
    /// <summary>
    /// Geocodes <paramref name="dto"/>, creates the place, and marks the country as visited.
    /// </summary>
    Task<PlaceDto> AddAsync(AddPlaceDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates place user-editable fields and syncs UserCountries.IsHome when IsHome changes.
    /// </summary>
    Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes a place and manages related country flags:
    /// - Unsets country IsVisited if this was the last place in the country.
    /// - Returns PromptHomeCountry=true if the deleted place was the user's home city,
    ///   so the frontend can ask whether the country is still the home country.
    /// </summary>
    Task<DeletePlaceResult> DeleteAsync(int placeId, CancellationToken ct = default);
}
