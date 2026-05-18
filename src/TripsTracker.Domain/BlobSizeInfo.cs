namespace TripsTracker.Domain;

public class BlobSizeInfo
{
    public required string BlobName { get; init; }
    public long SizeBytes { get; init; }
}
