using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class MapFunctions(IPlaceBusiness places, ICountryBusiness countries, IVisitedStateBusiness states)
{
    [Function("GetPlaces")]
    public async Task<IActionResult> GetPlaces(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "places")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await places.GetAllAsync(ct));

    [Function("GetCountries")]
    public async Task<IActionResult> GetCountries(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "countries")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await countries.GetAllAsync(ct));

    [Function("GetVisitedStates")]
    public async Task<IActionResult> GetVisitedStates(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "visited-states")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await states.GetAllAsync(ct));
}
