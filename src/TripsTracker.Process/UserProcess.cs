using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class UserProcess : IUserProcess
{
    private readonly IUserBusiness _users;
    private readonly ICountryBusiness _countries;

    public UserProcess(IUserBusiness users, ICountryBusiness countries)
    {
        _users = users;
        _countries = countries;
    }

    public async Task<UserDto> EnsureUserAsync(string email, string? displayName, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(email, ct);
        if (existing is not null) return existing;

        var newUser = await _users.CreateAsync(email, displayName, ct);
        await _users.AdoptOrphanedPlacesAsync(newUser.Id, ct);
        return newUser;
    }

    public async Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var updated = await _users.UpdateAsync(userId, dto, ct);
        if (updated is null) return null;

        if (dto.HomeCountryId.HasValue)
        {
            var country = await _countries.GetByIdAsync(dto.HomeCountryId.Value, ct);
            if (country is null) throw new NotFoundException("Country", dto.HomeCountryId.Value);
            if (!country.IsVisited) throw new BusinessRuleException("Home country must be a country you have visited.");
            await _countries.SetHomeAsync(dto.HomeCountryId.Value, true, ct);
        }

        return updated;
    }
}
