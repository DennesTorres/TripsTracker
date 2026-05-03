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
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(u => u.Email == email)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<UserDto> CreateAsync(string email, string? displayName, CancellationToken ct = default)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
        };
        await InsertAsync(user, ct);
        return new UserDto(user.Id, user.Email, user.DisplayName, user.CreatedAt);
    }

    public async Task<UserDto?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            u => u.Id == userId,
            s => s.SetProperty(u => u.DisplayName, dto.DisplayName),
            ct);
        if (rows == 0) return null;
        return await BuildBaseQuery()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task AdoptOrphanedPlacesAsync(int userId, CancellationToken ct = default)
    {
        var orphanedPlaces = await Context.Set<Place>()
            .Where(p => p.UserId == 0)
            .ToListAsync(ct);

        if (orphanedPlaces.Count == 0) return;

        await Context.Set<Place>()
            .Where(p => p.UserId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UserId, userId), ct);

        var countryIds = orphanedPlaces.Select(p => p.CountryId).Distinct();
        foreach (var countryId in countryIds)
        {
            var existing = await Context.Set<UserCountry>()
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CountryId == countryId, ct);

            if (existing is null)
            {
                Context.Set<UserCountry>().Add(new UserCountry
                {
                    UserId = userId,
                    CountryId = countryId,
                    IsVisited = true,
                    IsHome = false,
                    ShowStateBorders = false,
                });
            }
            else
            {
                existing.IsVisited = true;
            }
        }
        await Context.SaveChangesAsync(ct);
    }
}
