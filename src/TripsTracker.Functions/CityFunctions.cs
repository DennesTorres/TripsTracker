using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class CityFunctions(IGeocodingBusiness geocoding)
{
    [Function("SuggestCities")]
    public async Task<IActionResult> SuggestCities(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "cities/suggest")] HttpRequest req,
        CancellationToken ct)
    {
        var query = req.Query["q"].FirstOrDefault() ?? string.Empty;
        if (query.Length < 3)
            return new OkObjectResult(Array.Empty<object>());

        var results = await geocoding.SuggestCitiesAsync(query, ct);
        return new OkObjectResult(results);
    }
}
