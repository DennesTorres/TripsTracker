namespace TripsTracker.Domain;

public record ShareLinkDto(int Id, string Token, bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt, int ViewCount, int OwnerId);
public record CreateShareLinkDto(DateTime? ExpiresAt = null);
public record PublicMapDto(
    string OwnerDisplayName,
    List<PlaceDto> Places,
    List<CountryDto> Countries,
    List<VisitedStateDto> VisitedStates);
public record PublicShareSummaryDto(string Token, string DisplayName, int ContinentsVisited, int CountriesVisited, int PlacesCount);
