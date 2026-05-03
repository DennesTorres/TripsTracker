using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPlaceBusiness
{
    Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default);
    Task<PlaceDto> CreateAsync(CreatePlaceDto dto, CancellationToken ct = default);
    Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> HasAnyInCountryAsync(int countryId, CancellationToken ct = default);
    Task<bool> HasHomeInCountryAsync(int countryId, CancellationToken ct = default);
    Task<List<PlaceDto>> GetAllForUserAsync(int userId, CancellationToken ct = default);
    Task<bool> HasAnyForCurrentUserInRegionAsync(string region, CancellationToken ct = default);
    Task<bool> HasAnyGloballyInCountryAsync(int countryId, CancellationToken ct = default);
    Task<bool> HasAnyGloballyInRegionAsync(string region, CancellationToken ct = default);
}
