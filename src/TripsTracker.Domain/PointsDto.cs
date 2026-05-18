namespace TripsTracker.Domain;

public class PointEventDto
{
    public int Id { get; init; }
    public required string EventType { get; init; }
    public int Points { get; init; }
    public int? ReferenceId { get; init; }
    public string? ReferenceType { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CityName { get; init; }
    public string? CountryName { get; init; }
    public string? ContinentName { get; init; }
}

public class UserPointsSummaryDto
{
    public int TotalPoints { get; init; }
    public required List<PointEventDto> RecentEvents { get; init; }
}

public class UserStatementDto
{
    public int UserId { get; init; }
    public required string DisplayName { get; init; }
    public int TotalPoints { get; init; }
    public required List<PointEventDto> Events { get; init; }
}

public class LeaderboardEntryDto
{
    public int UserId { get; init; }
    public int Rank { get; init; }
    public required string DisplayName { get; init; }
    public int TotalPoints { get; init; }
}
