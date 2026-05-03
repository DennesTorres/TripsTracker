namespace TripsTracker.Domain;

public record ShareLinkDto(int Id, string Token, bool IsActive, bool RequiresLogin, bool IsDiscoverable, DateTime CreatedAt, DateTime? ExpiresAt, int ViewCount);
public record CreateShareLinkDto(DateTime? ExpiresAt = null, bool RequiresLogin = false, bool IsDiscoverable = false);
public record PublicMapDto(
    string OwnerDisplayName,
    List<PlaceDto> Places,
    List<CountryDto> Countries,
    List<VisitedStateDto> VisitedStates);
public record PublicShareSummaryDto(string Token, string DisplayName, int ContinentsVisited, int CountriesVisited, int PlacesCount);
