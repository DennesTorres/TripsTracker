using System.Transactions;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class CommentProcess : ICommentProcess
{
    private readonly IPlaceCommentBusiness _comments;
    private readonly IPointsBusiness _points;

    public CommentProcess(IPlaceCommentBusiness comments, IPointsBusiness points)
    {
        _comments = comments;
        _points = points;
    }

    public async Task<PlaceCommentDto> CreateAsync(int placeId, string text, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        var result = await _comments.CreateAsync(placeId, text, ct);
        await _points.AwardAsync(result.UserId, "comment_added", 3, result.Id, "Comment", ct);
        scope.Complete();
        return result;
    }
}
