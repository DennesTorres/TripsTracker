using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class CommentFunctions(IPlaceCommentBusiness comments)
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

        var result = await comments.CreateAsync(placeId, dto.Text, ct);
        return new OkObjectResult(result);
    }

    [Function("CreateCommentReply")]
    public async Task<IActionResult> CreateReply(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "comments/{commentId:int}/replies")] HttpRequest req,
        int commentId,
        CancellationToken ct)
    {
        var dto = await req.ReadFromJsonAsync<CreateReplyDto>(ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Text))
            return new BadRequestObjectResult(new { error = "Reply text is required" });

        var result = await comments.CreateReplyAsync(commentId, dto.Text, ct);
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
