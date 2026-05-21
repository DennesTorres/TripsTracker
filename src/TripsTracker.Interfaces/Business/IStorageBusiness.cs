using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IStorageBusiness
{
    Task<StorageUsageDto> GetUsageAsync(int userId, CancellationToken ct = default);
    Task UpdateStorageAsync(int userId, long totalBytes, DateTime refreshedAt, CancellationToken ct = default);
}
