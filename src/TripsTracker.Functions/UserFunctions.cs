using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using TripsTracker.Functions.Middleware;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Functions;

public class UserFunctions(IUserBusiness userBusiness, UserContext userContext)
{
    [Function("GetMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
        CancellationToken ct)
    {
        // Middleware already ensured the user exists and populated userContext.
        // Fetch the full record (includes DisplayName, CreatedAt).
        var user = await userBusiness.GetByEmailAsync(userContext.Email!, ct);
        return new OkObjectResult(user);
    }
}
