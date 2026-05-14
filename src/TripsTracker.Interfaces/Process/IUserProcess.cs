using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IUserProcess
{
    /// <summary>
    /// Ensures the user record exists for the given email. Creates it on first login.
    /// Safe to call on every login — idempotent.
    /// </summary>
    Task<UserDto> EnsureUserAsync(string email, string? displayName, CancellationToken ct = default);

    /// <summary>
    /// Updates display name and/or home country. Validates that the requested home country
    /// has been visited before setting it. Returns null if the user is not found.
    /// </summary>
    Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default);
}
