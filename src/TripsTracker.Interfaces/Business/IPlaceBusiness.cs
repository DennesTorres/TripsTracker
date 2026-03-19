using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPlaceBusiness
{
    Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default);
    Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PlaceDto> CreateAsync(SavePlaceDto dto, CancellationToken ct = default);
    Task<PlaceDto?> UpdateAsync(int id, SavePlaceDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
