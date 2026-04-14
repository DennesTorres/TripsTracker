using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IUserProcess
{
    /// <summary>
    /// Ensures the user record exists for the given email. Creates it on first login.
    /// Safe to call on every login — idempotent.
    /// </summary>
    Task<UserDto> EnsureUserAsync(string email, string? displayName, CancellationToken ct = default);
}
