using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class PlaceFunctions(IPlaceBusiness places)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // POST /places is handled by AddPlaceProcess (Group 6 — geocoding-driven creation)

    [Function("UpdatePlace")]
    public async Task<IActionResult> UpdatePlace(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "places/{id:int}")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var dto = await JsonSerializer.DeserializeAsync<UpdatePlaceDto>(req.Body, JsonOptions, ct);
        if (dto is null) return new BadRequestObjectResult("Invalid request body.");
        var result = await places.UpdateAsync(id, dto, ct);
        return result is null ? new NotFoundResult() : new OkObjectResult(result);
    }

    [Function("DeletePlace")]
    public async Task<IActionResult> DeletePlace(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "places/{id:int}")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var deleted = await places.DeleteAsync(id, ct);
        return deleted ? new NoContentResult() : new NotFoundResult();
    }
}
