using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPointsBusiness
{
    Task AwardAsync(int userId, string eventType, int points, int? referenceId = null, string? referenceType = null, CancellationToken ct = default);
    Task<UserPointsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<List<PointEventDto>> GetRecentAsync(int count = 20, CancellationToken ct = default);
}
