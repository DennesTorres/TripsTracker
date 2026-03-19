using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class VisitedStateFunctions(IVisitedStateBusiness states)
{
    [Function("SetVisitedState")]
    public async Task<IActionResult> SetVisitedState(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "visited-states/{countryCode}/{stateAbbr}")] HttpRequest req,
        string countryCode,
        string stateAbbr,
        CancellationToken ct)
    {
        var result = await states.SetVisitedAsync(countryCode.ToUpperInvariant(), stateAbbr.ToUpperInvariant(), ct);
        return new OkObjectResult(result);
    }

    [Function("ClearVisitedState")]
    public async Task<IActionResult> ClearVisitedState(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "visited-states/{countryCode}/{stateAbbr}")] HttpRequest req,
        string countryCode,
        string stateAbbr,
        CancellationToken ct)
    {
        var cleared = await states.ClearVisitedAsync(countryCode.ToUpperInvariant(), stateAbbr.ToUpperInvariant(), ct);
        return cleared ? new NoContentResult() : new NotFoundResult();
    }
}
