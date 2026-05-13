namespace TripsTracker.Domain;

public record PointEventDto(int Id, string EventType, int Points, int? ReferenceId, string? ReferenceType, DateTime CreatedAt,
    string? CityName = null, string? CountryName = null, string? ContinentName = null);

public record UserPointsSummaryDto(int TotalPoints, List<PointEventDto> RecentEvents);

public record UserStatementDto(int UserId, string DisplayName, int TotalPoints, List<PointEventDto> Events);

public record LeaderboardEntryDto(int UserId, int Rank, string DisplayName, int TotalPoints);
