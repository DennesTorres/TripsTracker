using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace TripsTracker.Functions;

public class HealthFunctions
{
    /// <summary>
    /// Unauthenticated health check endpoint for smoke tests and monitoring.
    /// JwtValidationMiddleware skips this function by name.
    /// </summary>
    [Function("HealthCheck")]
    public IActionResult Check(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
        => new OkObjectResult(new { status = "healthy" });
}
