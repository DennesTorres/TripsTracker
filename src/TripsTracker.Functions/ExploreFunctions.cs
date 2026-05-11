using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class ExploreFunctions(IExploreBusiness explore)
{
    [Function("ExploreSearch")]
    public async Task<IActionResult> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "explore")] HttpRequest req,
        CancellationToken ct)
    {
        var query = req.Query["q"].ToString() ?? "";
        return new OkObjectResult(await explore.SearchAsync(query, ct));
    }

    [Function("ExploreContent")]
    public async Task<IActionResult> GetContent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "explore/content")] HttpRequest req,
        CancellationToken ct)
    {
        var city = req.Query["city"].ToString();
        if (!int.TryParse(req.Query["countryId"], out var countryId) || string.IsNullOrWhiteSpace(city))
            return new BadRequestObjectResult(new { error = "city and countryId are required" });

        return new OkObjectResult(await explore.GetContentAsync(city, countryId, ct));
    }
}
