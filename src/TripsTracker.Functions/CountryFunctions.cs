using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class CountryFunctions(ICountryBusiness countries)
{
    [Function("SetCountryHome")]
    public async Task<IActionResult> SetHome(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "countries/{id:int}/home")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var value = req.Query.ContainsKey("value") && req.Query["value"] == "false" ? false : true;
        var result = value
            ? await countries.SetAsHomeAsync(id, ct)
            : await countries.UnsetHomeAsync(id, ct);
        return result is not null ? new OkObjectResult(result) : new NotFoundResult();
    }

    [Function("SetCountryStateBorders")]
    public async Task<IActionResult> SetShowStateBorders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "countries/{id:int}/state-borders")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var show = !(req.Query.ContainsKey("value") && req.Query["value"] == "false");
        var result = await countries.SetShowStateBordersAsync(id, show, ct);
        return result is not null ? new OkObjectResult(result) : new NotFoundResult();
    }
}
