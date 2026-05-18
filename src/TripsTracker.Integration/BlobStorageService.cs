using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Integration;

public class BlobStorageService : IBlobStorageService
{
    private const string ContainerName = "place-photos";
    private readonly BlobServiceClient _client;

    public BlobStorageService(BlobServiceClient client)
    {
        _client = client;
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        var container = _client.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        return container;
    }

    public async Task UploadAsync(string blobName, Stream data, string contentType, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(data, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
    }

    public async Task<Stream?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient(blobName);
        if (!await blob.ExistsAsync(ct)) return null;
        var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return result.Value.Content;
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
