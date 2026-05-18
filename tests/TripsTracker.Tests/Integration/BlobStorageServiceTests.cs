using Azure.Storage.Blobs;
using TripsTracker.Integration;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Tests.Integration;

[TestClass]
public class BlobStorageServiceTests
{
    [TestMethod]
    public void BlobStorageService_CanBeInstantiated_WithDevStorageConnectionString()
    {
        var client = new BlobServiceClient("UseDevelopmentStorage=true");
        IBlobStorageService service = new BlobStorageService(client);
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void BlobStorageService_CanBeInstantiated_WithBlobServiceUri()
    {
        var client = new BlobServiceClient(new Uri("https://example.blob.core.windows.net"));
        IBlobStorageService service = new BlobStorageService(client);
        Assert.IsNotNull(service);
    }
}
