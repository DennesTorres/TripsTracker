using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Integration;

public interface IBlobStorageService
{
    Task UploadAsync(string blobName, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string blobName, CancellationToken ct = default);
    Task DeleteAsync(string blobName, CancellationToken ct = default);
    Task<IReadOnlyList<BlobSizeInfo>> GetUserBlobsAsync(int userId, CancellationToken ct = default);
}
