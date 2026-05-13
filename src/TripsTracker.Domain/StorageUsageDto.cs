namespace TripsTracker.Domain;

public record StorageUsageDto(long UsedBytes, long LimitBytes, DateTime? LastRefreshedAt);
