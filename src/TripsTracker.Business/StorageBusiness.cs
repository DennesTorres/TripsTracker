using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class StorageBusiness : BusinessBase<User>, IStorageBusiness
{
    private const long MaxStorageBytes = 10L * 1024 * 1024 * 1024;

    public StorageBusiness(TripsTrackerDbContext context) : base(context) { }

    public async Task<StorageUsageDto> GetUsageAsync(int userId, CancellationToken ct = default)
    {
        var row = await BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => new { u.StorageUsedBytes, u.StorageLastRefreshedAt })
            .FirstOrDefaultAsync(ct);

        return new StorageUsageDto { UsedBytes = row?.StorageUsedBytes ?? 0, LimitBytes = MaxStorageBytes, LastRefreshedAt = row?.StorageLastRefreshedAt };
    }

    public Task UpdateStorageAsync(int userId, long totalBytes, DateTime refreshedAt, CancellationToken ct = default)
        => ExecuteUpdateAsync(
            u => u.Id == userId,
            s =>
            {
                s.SetProperty(u => u.StorageUsedBytes, totalBytes);
                s.SetProperty(u => u.StorageLastRefreshedAt, refreshedAt);
            },
            ct);
}
