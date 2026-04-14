using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class UserProcess : IUserProcess
{
    private readonly IUserBusiness _users;

    public UserProcess(IUserBusiness users)
    {
        _users = users;
    }

    public async Task<UserDto> EnsureUserAsync(string email, string? displayName, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(email, ct);
        if (existing is not null) return existing;

        return await _users.CreateAsync(email, displayName, ct);
    }
}
