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

    public async Task RevokeAsync(int userId, string eventTypePrefix, int? referenceId, string? referenceType, CancellationToken ct = default)
    {
        // Find the most recent positive event matching prefix + reference
        var original = await BuildBaseQuery()
            .Where(e => e.UserId == userId
                && e.EventType.StartsWith(eventTypePrefix)
                && e.ReferenceId == referenceId
                && e.ReferenceType == referenceType
                && e.Points > 0)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (original == null)
            return;

        var revocation = new PointEvent
        {
            UserId = userId,
            EventType = original.EventType + "_revoked",
            Points = -original.Points,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            OriginalEventId = original.Id,
            CreatedAt = DateTime.UtcNow,
        };
        await InsertAsync(revocation, ct);

        await Context.Set<User>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.TotalPoints, u => u.TotalPoints - original.Points), ct);
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

    public async Task<List<PointEventDto>> GetRecentAsync(int count = 20, CancellationToken ct = default)
    {
        var revokedOriginalIds = await BuildBaseQuery()
            .Where(e => e.UserId == _userContext.UserId && e.OriginalEventId != null)
            .Select(e => e.OriginalEventId!.Value)
            .ToListAsync(ct);

        return await BuildBaseQuery()
            .Where(e => e.UserId == _userContext.UserId
                && !e.EventType.EndsWith("_revoked")
                && !revokedOriginalIds.Contains(e.Id))
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .Select(e => new PointEventDto(e.Id, e.EventType, e.Points, e.ReferenceId, e.ReferenceType, e.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task ReassignAsync(int userId, string eventTypePrefix, int? oldReferenceId, string? oldReferenceType,
        int? newReferenceId, string? newReferenceType, CancellationToken ct = default)
    {
        var original = await BuildBaseQuery()
            .Where(e => e.UserId == userId
                && e.EventType.StartsWith(eventTypePrefix)
                && e.ReferenceId == oldReferenceId
                && e.ReferenceType == oldReferenceType
                && e.Points > 0)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (original == null)
            return;

        await RevokeAsync(userId, eventTypePrefix, oldReferenceId, oldReferenceType, ct);
        await AwardAsync(userId, original.EventType, original.Points, newReferenceId, newReferenceType, ct);
    }

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
