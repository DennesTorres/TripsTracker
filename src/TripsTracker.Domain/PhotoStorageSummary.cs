namespace TripsTracker.Domain;

public class PhotoStorageSummary
{
    public int Id { get; init; }
    public required string BlobName { get; init; }
    public long SizeBytes { get; init; }
}
