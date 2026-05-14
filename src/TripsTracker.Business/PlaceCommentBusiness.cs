using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Business;

public class PlaceCommentBusiness : BusinessBase<PlaceComment>, IPlaceCommentBusiness
{
    private readonly IUserContext _userContext;

    public PlaceCommentBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public async Task<PlaceCommentDto> CreateAsync(int placeId, string text, CancellationToken ct = default)
    {
        var comment = new PlaceComment
        {
            PlaceId = placeId,
            UserId = _userContext.UserId!.Value,
            Text = text,
            CreatedAt = DateTime.UtcNow,
        };
        await InsertAsync(comment, ct);

        var user = await Context.Set<User>().AsNoTracking()
            .Where(u => u.Id == comment.UserId)
            .Select(u => u.DisplayName ?? u.Email)
            .FirstOrDefaultAsync(ct);

        return new PlaceCommentDto(comment.Id, comment.PlaceId, comment.UserId,
            user ?? "", comment.Text, comment.CreatedAt, null, 0, 0, null);
    }

    public async Task<PlaceCommentDto> CreateReplyAsync(int parentCommentId, string text, CancellationToken ct = default)
    {
        var parent = await BuildBaseQuery()
            .Where(c => c.Id == parentCommentId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Comment", parentCommentId);

        var comment = new PlaceComment
        {
            PlaceId = parent.PlaceId,
            UserId = _userContext.UserId!.Value,
            ParentCommentId = parentCommentId,
            Text = text,
            CreatedAt = DateTime.UtcNow,
        };
        await InsertAsync(comment, ct);

        var user = await Context.Set<User>().AsNoTracking()
            .Where(u => u.Id == comment.UserId)
            .Select(u => u.DisplayName ?? u.Email)
            .FirstOrDefaultAsync(ct);

        return new PlaceCommentDto(comment.Id, comment.PlaceId, comment.UserId,
            user ?? "", comment.Text, comment.CreatedAt, null, 0, 0, parentCommentId);
    }

    public Task<List<PlaceCommentDto>> GetByPlaceAsync(int placeId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(c => c.PlaceId == placeId)
            .OrderByDescending(c => c.CreatedAt)
            .Join(Context.Set<User>().AsNoTracking(),
                c => c.UserId,
                u => u.Id,
                (c, u) => new { c, DisplayName = u.DisplayName ?? u.Email })
            .GroupJoin(
                Context.Set<CommentRating>().AsNoTracking(),
                x => x.c.Id,
                r => r.CommentId,
                (x, ratings) => new { x.c, x.DisplayName, ratings })
            .Select(x => new PlaceCommentDto(
                x.c.Id, x.c.PlaceId, x.c.UserId, x.DisplayName,
                x.c.Text, x.c.CreatedAt, x.c.UpdatedAt,
                x.ratings.Count(r => r.IsUpvote),
                x.ratings.Count(r => !r.IsUpvote),
                x.c.ParentCommentId))
            .ToListAsync(ct);

    public async Task<PlaceCommentDto?> UpdateAsync(int commentId, string text, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            c => c.Id == commentId && c.UserId == _userContext.UserId,
            s =>
            {
                s.SetProperty(c => c.Text, text);
                s.SetProperty(c => c.UpdatedAt, DateTime.UtcNow);
            },
            ct);
        return rows == 0 ? null : await GetByIdAsync(commentId, ct);
    }

    public async Task<bool> DeleteAsync(int commentId, CancellationToken ct = default)
    {
        var rows = await ExecuteDeleteAsync(c => c.Id == commentId && c.UserId == _userContext.UserId, ct);
        return rows > 0;
    }

    public async Task VoteAsync(int commentId, bool isUpvote, CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var existing = await Context.Set<CommentRating>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.CommentId == commentId, ct);
        if (existing != null)
        {
            existing.IsUpvote = isUpvote;
            await Context.SaveChangesAsync(ct);
        }
        else
        {
            try
            {
                Context.Set<CommentRating>().Add(new CommentRating
                {
                    UserId = userId,
                    CommentId = commentId,
                    IsUpvote = isUpvote,
                    CreatedAt = DateTime.UtcNow,
                });
                await Context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) { /* concurrent insert -- already voted */ }
        }
    }

    private Task<PlaceCommentDto?> GetByIdAsync(int commentId, CancellationToken ct)
        => BuildBaseQuery()
            .Where(c => c.Id == commentId)
            .Join(Context.Set<User>().AsNoTracking(),
                c => c.UserId,
                u => u.Id,
                (c, u) => new { c, DisplayName = u.DisplayName ?? u.Email })
            .GroupJoin(
                Context.Set<CommentRating>().AsNoTracking(),
                x => x.c.Id,
                r => r.CommentId,
                (x, ratings) => new { x.c, x.DisplayName, ratings })
            .Select(x => new PlaceCommentDto(
                x.c.Id, x.c.PlaceId, x.c.UserId, x.DisplayName,
                x.c.Text, x.c.CreatedAt, x.c.UpdatedAt,
                x.ratings.Count(r => r.IsUpvote),
                x.ratings.Count(r => !r.IsUpvote),
                x.c.ParentCommentId))
            .FirstOrDefaultAsync(ct);
}
