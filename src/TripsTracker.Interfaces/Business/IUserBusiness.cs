using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IUserBusiness
{
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserDto> CreateAsync(string email, string? displayName, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken ct = default);
}
