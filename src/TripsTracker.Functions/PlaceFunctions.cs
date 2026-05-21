using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Functions;

public class PlaceFunctions(IPlacesProcess placesProcess)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("CreatePlace")]
    public async Task<IActionResult> CreatePlace(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "places")] HttpRequest req,
        CancellationToken ct)
    {
        var dto = await JsonSerializer.DeserializeAsync<AddPlaceDto>(req.Body, JsonOptions, ct);
        if (dto is null) return new BadRequestObjectResult("Invalid request body.");
        try
        {
            var result = await placesProcess.AddAsync(dto, ct);
            return new CreatedResult($"/api/places/{result.Id}", result);
        }
        catch (NotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (GeocodingMismatchException ex)
        {
            return new UnprocessableEntityObjectResult(new
            {
                errorCode = ex.ErrorCode,
                message = ex.Message,
                suggestedCity = ex.SuggestedCity,
            });
        }
        catch (BusinessRuleException ex)
        {
            return new UnprocessableEntityObjectResult(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
    }

    [Function("UpdatePlace")]
    public async Task<IActionResult> UpdatePlace(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "places/{id:int}")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var dto = await JsonSerializer.DeserializeAsync<UpdatePlaceDto>(req.Body, JsonOptions, ct);
        if (dto is null) return new BadRequestObjectResult("Invalid request body.");
        var result = await placesProcess.UpdateAsync(id, dto, ct);
        return result is null ? new NotFoundResult() : new OkObjectResult(result);
    }

    [Function("DeletePlace")]
    public async Task<IActionResult> DeletePlace(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "places/{id:int}")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        try
        {
            var result = await placesProcess.DeleteAsync(id, ct);
            return new OkObjectResult(result);
        }
        catch (NotFoundException)
        {
            return new NotFoundResult();
        }
    }
}
