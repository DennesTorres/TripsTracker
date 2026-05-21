using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Transactions;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class CommentFunctions(IPlaceCommentBusiness comments, IPointsBusiness points)
{
    [Function("GetPlaceComments")]
    public async Task<IActionResult> GetByPlace(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "places/{placeId:int}/comments")] HttpRequest req,
        int placeId,
        CancellationToken ct)
        => new OkObjectResult(await comments.GetByPlaceAsync(placeId, ct));

    [Function("CreateComment")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "places/{placeId:int}/comments")] HttpRequest req,
        int placeId,
        CancellationToken ct)
    {
        var dto = await req.ReadFromJsonAsync<CreateCommentDto>(ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Text))
            return new BadRequestObjectResult(new { error = "Comment text is required" });

        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        var result = await comments.CreateAsync(placeId, dto.Text, ct);
        await points.AwardAsync(result.UserId, "comment_added", 3, result.Id, "Comment", ct);
        scope.Complete();
        return new OkObjectResult(result);
    }

    [Function("UpdateComment")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "comments/{commentId:int}")] HttpRequest req,
        int commentId,
        CancellationToken ct)
    {
        var dto = await req.ReadFromJsonAsync<CreateCommentDto>(ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Text))
            return new BadRequestObjectResult(new { error = "Comment text is required" });

        var result = await comments.UpdateAsync(commentId, dto.Text, ct);
        return result is not null ? new OkObjectResult(result) : new NotFoundResult();
    }

    [Function("DeleteComment")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "comments/{commentId:int}")] HttpRequest req,
        int commentId,
        CancellationToken ct)
    {
        var ok = await comments.DeleteAsync(commentId, ct);
        return ok ? new OkResult() : new NotFoundResult();
    }

    [Function("VoteComment")]
    public async Task<IActionResult> Vote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "comments/{commentId:int}/vote")] HttpRequest req,
        int commentId,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<VoteBody>(ct);
        if (body is null)
            return new BadRequestObjectResult(new { error = "isUpvote is required" });

        await comments.VoteAsync(commentId, body.IsUpvote, ct);
        return new OkResult();
    }

    private record VoteBody(bool IsUpvote);
}
