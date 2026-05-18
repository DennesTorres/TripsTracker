using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPointsBusiness
{
    Task AwardAsync(int userId, string eventType, int points, int? referenceId = null, string? referenceType = null, CancellationToken ct = default);
    Task RevokeAsync(int userId, string eventTypePrefix, int? referenceId, string? referenceType, CancellationToken ct = default);
    Task ReassignAsync(int userId, string eventTypePrefix, int? oldReferenceId, string? oldReferenceType, int? newReferenceId, string? newReferenceType, CancellationToken ct = default);
    Task<UserPointsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<List<PointEventDto>> GetRecentAsync(int count = 20, CancellationToken ct = default);
    Task<UserStatementDto> GetStatementAsync(int userId, CancellationToken ct = default);
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int limit = 20, CancellationToken ct = default);
}
