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
        if (query.Length < 2)
            return new OkObjectResult(Array.Empty<object>());

        var countryCode = req.Query["country"].FirstOrDefault() ?? string.Empty;
        var results = await geocoding.SuggestCitiesAsync(query, countryCode, ct);
        return new OkObjectResult(results);
    }
}
