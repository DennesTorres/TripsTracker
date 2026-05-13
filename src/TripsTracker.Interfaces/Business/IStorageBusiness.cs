using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IStorageBusiness
{
    Task<StorageUsageDto> GetUsageAsync(int userId, CancellationToken ct = default);
    Task<StorageUsageDto> RefreshAsync(int userId, CancellationToken ct = default);
}
