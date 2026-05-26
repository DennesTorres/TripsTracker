using Azure;
using Azure.Storage.Blobs;
using System.Text;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Integration;

public class BlobBorderCacheService : IBorderCacheService
{
    private readonly BlobServiceClient _client;
    private const string ContainerName = "borders";

    public BlobBorderCacheService(BlobServiceClient client)
    {
        _client = client;
    }

    public async Task<string?> GetAsync(string isoAlpha3, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(ContainerName);
        var blob = container.GetBlobClient($"{isoAlpha3}.json");
        try
        {
            var response = await blob.DownloadContentAsync(ct);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetAsync(string isoAlpha3, string json, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient($"{isoAlpha3}.json");
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }
}
