using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IUserBusiness
{
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<UserDto> CreateAsync(string email, string? displayName, CancellationToken ct = default);
    Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task AdoptOrphanedPlacesAsync(int userId, CancellationToken ct = default);
    Task<long> GetStorageUsedAsync(int userId, CancellationToken ct = default);
    Task AddStorageUsedAsync(int userId, long deltaBytes, CancellationToken ct = default);
}
