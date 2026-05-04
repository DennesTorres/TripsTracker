using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class PointsFunctions(IPointsBusiness points)
{
    [Function("GetPointsSummary")]
    public async Task<IActionResult> GetSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/points")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await points.GetSummaryAsync(ct));

    [Function("GetRecentPoints")]
    public async Task<IActionResult> GetRecent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/points/recent")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await points.GetRecentAsync(20, ct));

    [Function("GetLeaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "leaderboard")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await points.GetLeaderboardAsync(20, ct));
}
