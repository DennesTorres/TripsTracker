using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class ShareLinkBusiness : BusinessBase<ShareLink>, IShareLinkBusiness
{
    private readonly IUserContext _userContext;

    public ShareLinkBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public async Task<ShareLinkDto> CreateAsync(CreateShareLinkDto dto, CancellationToken ct = default)
    {
        var token = GenerateToken();
        var link = new ShareLink
        {
            UserId = _userContext.UserId!.Value,
            Token = token,
            IsActive = true,
            RequiresLogin = dto.RequiresLogin,
            // RequiresLogin links are never discoverable regardless of the flag
            IsDiscoverable = dto.IsDiscoverable && !dto.RequiresLogin,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt,
        };
        await InsertAsync(link, ct);
        return ToDto(link);
    }

    public Task<List<ShareLinkDto>> GetUserLinksAsync(CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(l => l.UserId == _userContext.UserId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ShareLinkDto(l.Id, l.Token, l.IsActive, l.RequiresLogin, l.IsDiscoverable, l.CreatedAt, l.ExpiresAt, l.ViewCount))
            .ToListAsync(ct);

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            l => l.Id == id && l.UserId == _userContext.UserId,
            s => s.SetProperty(l => l.IsActive, false),
            ct);
        return rows > 0;
    }

    public Task<ShareLinkDto?> GetByTokenAsync(string token, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(l => l.Token == token && l.IsActive && (l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow))
            .Select(l => new ShareLinkDto(l.Id, l.Token, l.IsActive, l.RequiresLogin, l.IsDiscoverable, l.CreatedAt, l.ExpiresAt, l.ViewCount))
            .FirstOrDefaultAsync(ct);

    public Task IncrementViewCountAsync(string token, CancellationToken ct = default)
        => ExecuteUpdateAsync(
            l => l.Token == token,
            s => s.SetProperty(l => l.ViewCount, l => l.ViewCount + 1),
            ct);

    public Task<int?> GetOwnerIdAsync(string token, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(l => l.Token == token)
            .Select(l => (int?)l.UserId)
            .FirstOrDefaultAsync(ct);

    public Task<List<PublicShareSummaryDto>> DiscoverAsync(string query, int limit = 20, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(l => l.IsActive && l.IsDiscoverable && (l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow))
            .Join(Context.Set<User>(), l => l.UserId, u => u.Id, (l, u) => new { l, u })
            .Where(x => string.IsNullOrEmpty(query) || (x.u.DisplayName != null && x.u.DisplayName.Contains(query)))
            .Select(x => new
            {
                x.l.Token,
                DisplayName = x.u.DisplayName ?? x.u.Email,
                CountriesVisited = Context.Set<UserCountry>().Count(uc => uc.UserId == x.l.UserId && uc.IsVisited),
                PlacesCount = Context.Set<Place>().Count(p => p.UserId == x.l.UserId),
            })
            .OrderByDescending(x => x.CountriesVisited)
            .ThenByDescending(x => x.PlacesCount)
            .Take(limit)
            .Select(x => new PublicShareSummaryDto(x.Token, x.DisplayName, x.CountriesVisited, x.PlacesCount))
            .ToListAsync(ct);

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private static ShareLinkDto ToDto(ShareLink l)
        => new(l.Id, l.Token, l.IsActive, l.RequiresLogin, l.IsDiscoverable, l.CreatedAt, l.ExpiresAt, l.ViewCount);
}
