using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Domain;
using TripsTracker.Functions.Middleware;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Functions;

public class UserFunctions(IUserBusiness userBusiness, IUserProcess userProcess, UserContext userContext)
{
    [Function("GetMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
        CancellationToken ct)
    {
        var user = await userBusiness.GetByEmailAsync(userContext.Email!, ct);
        return new OkObjectResult(user);
    }

    [Function("UpdateMe")]
    public async Task<IActionResult> UpdateMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "me")] HttpRequest req,
        CancellationToken ct)
    {
        var dto = await req.ReadFromJsonAsync<UpdateUserDto>(ct);
        if (dto is null) return new BadRequestResult();

        var updated = await userProcess.UpdateAsync(userContext.UserId!.Value, dto, ct);
        return updated is not null ? new OkObjectResult(updated) : new NotFoundResult();
    }
}
