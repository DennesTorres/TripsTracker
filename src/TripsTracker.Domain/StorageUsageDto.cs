namespace TripsTracker.Domain;

public class StorageUsageDto
{
    public long UsedBytes { get; init; }
    public long LimitBytes { get; init; }
    public DateTime? LastRefreshedAt { get; init; }
}
