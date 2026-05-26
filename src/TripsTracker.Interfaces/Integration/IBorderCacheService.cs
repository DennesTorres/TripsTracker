namespace TripsTracker.Interfaces.Integration;

public interface IBorderCacheService
{
    Task<string?> GetAsync(string isoAlpha3, CancellationToken ct = default);
    Task SetAsync(string isoAlpha3, string json, CancellationToken ct = default);
}
