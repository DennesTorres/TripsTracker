using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IStorageProcess
{
    Task<StorageUsageDto> RefreshAsync(int userId, CancellationToken ct = default);
}
