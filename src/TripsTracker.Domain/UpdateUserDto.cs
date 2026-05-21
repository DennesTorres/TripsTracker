namespace TripsTracker.Domain;

public record UpdateUserDto(string? DisplayName, bool? IsDiscoverable = null);
