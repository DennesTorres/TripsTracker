namespace TripsTracker.Domain;

public record UpdateUserDto(string? DisplayName, int? HomeCountryId, bool? IsDiscoverable = null);
