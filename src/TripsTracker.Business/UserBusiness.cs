using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class UserBusiness : BusinessBase<User>, IUserBusiness
{
    public UserBusiness(TripsTrackerDbContext context) : base(context) { }

    public Task<UserDto?> GetByIdAsync(int userId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt, u.IsDiscoverable))
            .FirstOrDefaultAsync(ct);

    public Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(u => u.Email == email)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt, u.IsDiscoverable))
            .FirstOrDefaultAsync(ct);

    public async Task<UserDto> CreateAsync(string email, string? displayName, CancellationToken ct = default)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
        };
        await InsertAsync(user, ct);
        return new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt, user.IsDiscoverable);
    }

    public async Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            u => u.Id == userId,
            s =>
            {
                s.SetProperty(u => u.DisplayName, dto.DisplayName);
                if (dto.IsDiscoverable.HasValue)
                    s.SetProperty(u => u.IsDiscoverable, dto.IsDiscoverable.Value);
            },
            ct);
        if (rows == 0) return null;
        return await BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt, u.IsDiscoverable))
            .FirstOrDefaultAsync(ct);
    }

    public Task<long> GetStorageUsedAsync(int userId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => u.StorageUsedBytes)
            .FirstOrDefaultAsync(ct);

    public Task AddStorageUsedAsync(int userId, long deltaBytes, CancellationToken ct = default)
        => ExecuteUpdateAsync(
            u => u.Id == userId,
            s => s.SetProperty(u => u.StorageUsedBytes, u => u.StorageUsedBytes + deltaBytes),
            ct);
}
