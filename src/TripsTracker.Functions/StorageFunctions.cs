using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Functions.Middleware;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class StorageFunctions(IStorageBusiness storage, UserContext userContext)
{
    [Function("GetStorageUsage")]
    public async Task<IActionResult> GetUsage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "storage/usage")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await storage.GetUsageAsync(userContext.UserId!.Value, ct));

    [Function("RefreshStorageUsage")]
    public async Task<IActionResult> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "storage/refresh")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await storage.RefreshAsync(userContext.UserId!.Value, ct));
}
