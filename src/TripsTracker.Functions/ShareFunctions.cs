using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Functions;

public class ShareFunctions(IShareLinkBusiness shareLinks, IPublicMapProcess publicMap, IUserContext userContext)
{
    [Function("CreateShareLink")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "share-links")] HttpRequest req,
        CancellationToken ct)
    {
        var dto = await req.ReadFromJsonAsync<CreateShareLinkDto>(ct) ?? new CreateShareLinkDto();
        var link = await shareLinks.CreateAsync(dto, ct);
        return new OkObjectResult(link);
    }

    [Function("GetShareLinks")]
    public async Task<IActionResult> GetAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "share-links")] HttpRequest req,
        CancellationToken ct)
        => new OkObjectResult(await shareLinks.GetUserLinksAsync(ct));

    [Function("DeactivateShareLink")]
    public async Task<IActionResult> Deactivate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "share-links/{id:int}")] HttpRequest req,
        int id,
        CancellationToken ct)
    {
        var ok = await shareLinks.DeactivateAsync(id, ct);
        return ok ? new OkResult() : new NotFoundResult();
    }

    /// <summary>
    /// Public endpoint — no authentication required.
    /// JwtValidationMiddleware skips this function by name.
    /// </summary>
    [Function("GetSharedMap")]
    public async Task<IActionResult> GetSharedMap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shared/{token}")] HttpRequest req,
        string token,
        CancellationToken ct)
    {
        var link = await shareLinks.GetByTokenAsync(token, ct);
        if (link is null) return new NotFoundResult();

        if (link.RequiresLogin && userContext.UserId is null)
            return new UnauthorizedResult();

        var data = await publicMap.GetSharedMapAsync(token, ct);
        return data is not null ? new OkObjectResult(data) : new NotFoundResult();
    }

    /// <summary>
    /// Discover public maps — returns active, non-login-required share links.
    /// JwtValidationMiddleware skips this function by name.
    /// </summary>
    [Function("DiscoverMaps")]
    public async Task<IActionResult> Discover(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "discover")] HttpRequest req,
        CancellationToken ct)
    {
        var query = req.Query["q"].ToString() ?? string.Empty;
        var results = await shareLinks.DiscoverAsync(query, ct: ct);
        return new OkObjectResult(results);
    }
}
