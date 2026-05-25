using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPlaceBusiness
{
    Task<List<VisitedStateDto>> GetVisitedStatesAsync(CancellationToken ct = default);
    Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default);
    Task<PlaceDto> CreateAsync(CreatePlaceDto dto, CancellationToken ct = default);
    Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> HasAnyInCountryAsync(int countryId, CancellationToken ct = default);
    Task<bool> HasHomeInCountryAsync(int countryId, CancellationToken ct = default);
    Task ClearAllHomePlacesAsync(CancellationToken ct = default);
    Task MarkAsHomeAsync(int placeId, CancellationToken ct = default);
    /// <summary>Returns coordinate+name data for a globally-stored place if it already exists, or null if geocoding is required.</summary>
    Task<CreatePlaceDto?> FindGlobalAsync(string city, int countryId, CancellationToken ct = default);
}
