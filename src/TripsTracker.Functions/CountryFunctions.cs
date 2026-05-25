using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Functions;

public class CountryFunctions(ICountryBusiness countries, ICountriesProcess countriesProcess)
{
    [Function("GetCountryBorders")]
    public async Task<IActionResult> GetBorders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "countries/{id:int}/borders")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var geoJson = await countriesProcess.GetBordersAsync(id, ct);
        if (geoJson is null) return new NotFoundResult();
        return new ContentResult { Content = geoJson, ContentType = "application/json", StatusCode = 200 };
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
