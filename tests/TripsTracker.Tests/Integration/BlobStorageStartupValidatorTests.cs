using Microsoft.Extensions.Configuration;
using TripsTracker.Integration;

namespace TripsTracker.Tests.Integration;

[TestClass]
public class BlobStorageStartupValidatorTests
{
    [TestMethod]
    public void Validate_ThrowsInvalidOperation_WhenBlobStorageSectionMissing()
    {
        var config = new ConfigurationBuilder().Build();
        bool threw = false;
        try { BlobStorageStartupValidator.Validate(config); }
        catch (InvalidOperationException) { threw = true; }
        Assert.IsTrue(threw, "Expected InvalidOperationException when BlobStorage section is missing");
    }

    [TestMethod]
    public void Validate_ThrowsInvalidOperation_WhenConnectionStringAndServiceUriEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BlobStorage:SomeOtherKey"] = "value" })
            .Build();
        bool threw = false;
        try { BlobStorageStartupValidator.Validate(config); }
        catch (InvalidOperationException) { threw = true; }
        Assert.IsTrue(threw, "Expected InvalidOperationException when ConnectionString and serviceUri are both empty");
    }

    [TestMethod]
    public void Validate_DoesNotThrow_WhenConnectionStringIsSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true" })
            .Build();
        BlobStorageStartupValidator.Validate(config); // no exception
    }

    [TestMethod]
    public void Validate_DoesNotThrow_WhenServiceUriIsSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BlobStorage:serviceUri"] = "https://account.blob.core.windows.net" })
            .Build();
        BlobStorageStartupValidator.Validate(config); // no exception
    }
}
