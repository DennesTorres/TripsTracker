using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class StorageProcess : IStorageProcess
{
    private readonly IPlacePhotoBusiness _photos;
    private readonly IBlobStorageService _blobs;
    private readonly IStorageBusiness _storage;

    public StorageProcess(IPlacePhotoBusiness photos, IBlobStorageService blobs, IStorageBusiness storage)
    {
        _photos = photos;
        _blobs = blobs;
        _storage = storage;
    }

    public async Task<StorageUsageDto> RefreshAsync(int userId, CancellationToken ct = default)
    {
        var blobInfos = await _blobs.GetUserBlobsAsync(userId, ct);
        var blobSizes = blobInfos.ToDictionary(b => b.BlobName, b => b.SizeBytes);

        var photoSummaries = await _photos.GetUserStorageSummaryAsync(userId, ct);

        foreach (var photo in photoSummaries)
        {
            if (blobSizes.TryGetValue(photo.BlobName, out var actualSize) && photo.SizeBytes != actualSize)
                await _photos.UpdateSizeAsync(photo.Id, actualSize, ct);
        }

        var totalBytes = photoSummaries.Sum(p =>
            blobSizes.TryGetValue(p.BlobName, out var sz) ? sz : p.SizeBytes);

        var now = DateTime.UtcNow;
        await _storage.UpdateStorageAsync(userId, totalBytes, now, ct);

        return await _storage.GetUsageAsync(userId, ct);
    }
}
