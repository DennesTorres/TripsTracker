using Microsoft.Extensions.Configuration;

namespace TripsTracker.Integration;

/// <summary>
/// Validates that BlobStorage configuration is present at application startup.
/// Throws <see cref="InvalidOperationException"/> with a clear message if the
/// required configuration (ConnectionString or serviceUri) is missing.
/// </summary>
public static class BlobStorageStartupValidator
{
    public static void Validate(IConfiguration config)
    {
        var section = config.GetSection("BlobStorage");
        var connectionString = section["ConnectionString"];
        var serviceUri = section["serviceUri"];

        if (string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(serviceUri))
            throw new InvalidOperationException(
                "BlobStorage configuration is missing. " +
                "For local development: add BlobStorage__ConnectionString=UseDevelopmentStorage=true to local.settings.json. " +
                "For Azure: add BlobStorage__serviceUri pointing to the storage account.");
    }
}
