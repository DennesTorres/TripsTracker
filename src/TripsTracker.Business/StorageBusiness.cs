using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Business;

public class StorageBusiness : BusinessBase<User>, IStorageBusiness
{
    private const long MaxStorageBytes = 10L * 1024 * 1024 * 1024;
    private readonly IBlobStorageService _blobs;

    public StorageBusiness(TripsTrackerDbContext context, IBlobStorageService blobs) : base(context)
    {
        _blobs = blobs;
    }

    public async Task<StorageUsageDto> GetUsageAsync(int userId, CancellationToken ct = default)
    {
        var row = await BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => new { u.StorageUsedBytes, u.StorageLastRefreshedAt })
            .FirstOrDefaultAsync(ct);

        return new StorageUsageDto(row?.StorageUsedBytes ?? 0, MaxStorageBytes, row?.StorageLastRefreshedAt);
    }

    public async Task<StorageUsageDto> RefreshAsync(int userId, CancellationToken ct = default)
    {
        var blobInfos = await _blobs.GetUserBlobsAsync(userId, ct);
        var blobSizes = blobInfos.ToDictionary(b => b.BlobName, b => b.SizeBytes);

        var photos = await Context.Set<PlacePhoto>()
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.BlobName, p.SizeBytes })
            .ToListAsync(ct);

        foreach (var photo in photos)
        {
            if (blobSizes.TryGetValue(photo.BlobName, out var actualSize) && photo.SizeBytes != actualSize)
            {
                await Context.Set<PlacePhoto>()
                    .Where(p => p.Id == photo.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.SizeBytes, actualSize), ct);
            }
        }

        var totalBytes = photos.Sum(p =>
            blobSizes.TryGetValue(p.BlobName, out var sz) ? sz : p.SizeBytes);

        var now = DateTime.UtcNow;
        await ExecuteUpdateAsync(
            u => u.Id == userId,
            s =>
            {
                s.SetProperty(u => u.StorageUsedBytes, totalBytes);
                s.SetProperty(u => u.StorageLastRefreshedAt, now);
            },
            ct);

        return new StorageUsageDto(totalBytes, MaxStorageBytes, now);
    }
}
