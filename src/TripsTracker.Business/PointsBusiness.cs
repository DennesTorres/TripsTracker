using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class PointsBusiness : BusinessBase<PointEvent>, IPointsBusiness
{
    private readonly IUserContext _userContext;

    public PointsBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public async Task AwardAsync(int userId, string eventType, int points,
        int? referenceId = null, string? referenceType = null, CancellationToken ct = default)
    {
        var evt = new PointEvent
        {
            UserId = userId,
            EventType = eventType,
            Points = points,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            CreatedAt = DateTime.UtcNow,
        };
        await InsertAsync(evt, ct);

        // Update cached total on Users table
        await Context.Set<User>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalPoints, u => u.TotalPoints + points), ct);
    }

    public async Task<UserPointsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var total = await Context.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.TotalPoints)
            .FirstOrDefaultAsync(ct);

        var recent = await GetRecentAsync(10, ct);
        return new UserPointsSummaryDto(total, recent);
    }

    public Task<List<PointEventDto>> GetRecentAsync(int count = 20, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(e => e.UserId == _userContext.UserId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .Select(e => new PointEventDto(e.Id, e.EventType, e.Points, e.ReferenceId, e.ReferenceType, e.CreatedAt))
            .ToListAsync(ct);

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int limit = 20, CancellationToken ct = default)
    {
        var rows = await Context.Set<User>().AsNoTracking()
            .Where(u => u.TotalPoints > 0)
            .OrderByDescending(u => u.TotalPoints)
            .Take(limit)
            .Select(u => new { DisplayName = u.DisplayName ?? u.Email, u.TotalPoints })
            .ToListAsync(ct);

        return rows.Select((r, i) => new LeaderboardEntryDto(i + 1, r.DisplayName, r.TotalPoints)).ToList();
    }
}
