using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class UserBusiness : BusinessBase<User>, IUserBusiness
{
    public UserBusiness(TripsTrackerDbContext context) : base(context) { }

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
}
