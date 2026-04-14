namespace TripsTracker.Domain;

public record PointEventDto(int Id, string EventType, int Points, int? ReferenceId, string? ReferenceType, DateTime CreatedAt);

public record UserPointsSummaryDto(int TotalPoints, List<PointEventDto> RecentEvents);
