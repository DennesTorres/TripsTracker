namespace TripsTracker.Domain;

public class ShareLinkDto
{
    public int Id { get; init; }
    public required string Token { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int ViewCount { get; init; }
    public int OwnerId { get; init; }
}

public class CreateShareLinkDto
{
    public DateTime? ExpiresAt { get; init; }
}

public class PublicMapDto
{
    public required string OwnerDisplayName { get; init; }
    public required List<PlaceDto> Places { get; init; }
    public required List<CountryDto> Countries { get; init; }
    public required List<VisitedStateDto> VisitedStates { get; init; }
}

public class PublicShareSummaryDto
{
    public required string Token { get; init; }
    public required string DisplayName { get; init; }
    public int ContinentsVisited { get; init; }
    public int CountriesVisited { get; init; }
    public int PlacesCount { get; init; }
}
