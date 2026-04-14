namespace TripsTracker.Domain;

public record UserDto(int Id, string Email, string? DisplayName, DateTime CreatedAt);
