using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class PhotoFunctions(IPlacePhotoBusiness photos)
{
    [Function("GetPlacePhotos")]
    public async Task<IActionResult> GetByPlace(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "places/{placeId:int}/photos")] HttpRequest req,
        int placeId,
        CancellationToken ct)
        => new OkObjectResult(await photos.GetByPlaceAsync(placeId, ct));

    [Function("UploadPhoto")]
    public async Task<IActionResult> Upload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "places/{placeId:int}/photos")] HttpRequest req,
        int placeId,
        CancellationToken ct)
    {
        var form = await req.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
            return new BadRequestObjectResult(new { error = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return new BadRequestObjectResult(new { error = "File too large (max 10MB)" });

        var blobName = $"{placeId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var caption = form.TryGetValue("caption", out var c) ? c.ToString() : null;

        // For now, store blob name only — actual blob storage integration in Bicep deployment
        var result = await photos.CreateAsync(placeId, blobName, file.FileName, file.ContentType, file.Length, caption, ct);
        return new OkObjectResult(result);
    }

    [Function("DeletePhoto")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "photos/{photoId:int}")] HttpRequest req,
        int photoId,
        CancellationToken ct)
    {
        var ok = await photos.DeleteAsync(photoId, ct);
        return ok ? new OkResult() : new NotFoundResult();
    }

    [Function("RatePhoto")]
    public async Task<IActionResult> Rate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "photos/{photoId:int}/rating")] HttpRequest req,
        int photoId,
        CancellationToken ct)
    {
        var body = await req.ReadFromJsonAsync<RatingBody>(ct);
        if (body is null || body.Rating < 1 || body.Rating > 5)
            return new BadRequestObjectResult(new { error = "Rating must be 1-5" });

        await photos.RateAsync(photoId, body.Rating, ct);
        return new OkResult();
    }

    private record RatingBody(byte Rating);
}
