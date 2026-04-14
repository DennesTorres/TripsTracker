namespace TripsTracker.Domain;

public record ShareLinkDto(int Id, string Token, bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt, int ViewCount);
public record CreateShareLinkDto(DateTime? ExpiresAt = null);
public record PublicMapDto(
    string OwnerDisplayName,
    List<PlaceDto> Places,
    List<CountryDto> Countries,
    List<VisitedStateDto> VisitedStates);
