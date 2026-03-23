using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class CountryFunctions(ICountryBusiness countries)
{
    [Function("SetCountryVisited")]
    public async Task<IActionResult> SetCountryVisited(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "countries/{id:int}/visited")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        bool isVisited = req.Query["value"] != "false";
        var result = await countries.SetVisitedAsync(id, isVisited, ct);
        return result is null ? new NotFoundResult() : new OkObjectResult(result);
    }

    [Function("SetCountryHome")]
    public async Task<IActionResult> SetCountryHome(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "countries/{id:int}/home")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        bool isHome = req.Query["value"] != "false";
        var result = await countries.SetHomeAsync(id, isHome, ct);
        return result is null ? new NotFoundResult() : new OkObjectResult(result);
    }
}
