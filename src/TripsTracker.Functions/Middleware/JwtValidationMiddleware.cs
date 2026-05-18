using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Functions.Middleware;

/// <summary>
/// Functions worker middleware that validates the JWT Bearer token on every request.
/// Extracts the user's email claim, ensures the user record exists via IUserProcess,
/// and populates the scoped UserContext with the authenticated user's int ID.
/// Returns HTTP 401 immediately if the token is missing, invalid, or has no email claim.
///
/// Uses FunctionContext.GetHttpRequestDataAsync() to read the request because
/// IHttpContextAccessor.HttpContext is null in worker middleware — the ASP.NET Core
/// integration sets HttpContext only after worker middleware has already run.
/// </summary>
public class JwtValidationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly JwtValidatorService _jwtValidator;

    public JwtValidationMiddleware(JwtValidatorService jwtValidator)
    {
        _jwtValidator = jwtValidator;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var logger = context.GetLogger<JwtValidationMiddleware>();
        var request = await context.GetHttpRequestDataAsync();

        if (request is null)
        {
            // Non-HTTP trigger — skip auth
            await next(context);
            return;
        }

        // Public endpoints that do not require authentication
        var functionName = context.FunctionDefinition.Name;
        if (functionName is "HealthCheck" or "GetSharedMap")
        {
            await next(context);
            return;
        }

        var authHeader = request.Headers.TryGetValues("Authorization", out var values)
            ? values.FirstOrDefault()
            : null;

        var userContext = context.InstanceServices.GetRequiredService<UserContext>();

        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            var token = authHeader[7..];
            var principal = await _jwtValidator.ValidateAsync(token, context.CancellationToken);

            if (principal is null)
            {
                logger.LogWarning("Token validation failed for {Function} — token present but invalid", context.FunctionDefinition.Name);
            }
            else
            {
                var email = principal.FindFirst("email")?.Value
                         ?? principal.FindFirst("preferred_username")?.Value;

                if (email is null)
                {
                    var claimTypes = string.Join(", ", principal.Claims.Select(c => c.Type));
                    logger.LogWarning("Token valid but no email claim found for {Function}. Available claims: {Claims}", context.FunctionDefinition.Name, claimTypes);
                }
                else
                {
                    try
                    {
                        var displayName = principal.FindFirst("name")?.Value;
                        var userProcess = context.InstanceServices.GetRequiredService<IUserProcess>();
                        var user = await userProcess.EnsureUserAsync(email, displayName, context.CancellationToken);

                        userContext.UserId = user.Id;
                        userContext.Email = user.Email;
                        logger.LogInformation("Authenticated {Email} → UserId {UserId} for {Function}", email, user.Id, context.FunctionDefinition.Name);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "User resolution failed for {Email}", email);
                        var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                        errorResponse.Headers.Add("Content-Type", "application/json");
                        await errorResponse.WriteStringAsync($"{{\"error\":\"User resolution failed: {ex.Message}\"}}");
                        context.GetInvocationResult().Value = errorResponse;
                        return;
                    }
                }
            }
        }
        else
        {
            logger.LogWarning("No Bearer token for {Function}", context.FunctionDefinition.Name);
        }

        if (!userContext.IsAuthenticated)
        {
            logger.LogWarning("Returning 401 for {Function} — auth pipeline completed without establishing identity", context.FunctionDefinition.Name);
            var unauthorizedResponse = request.CreateResponse(HttpStatusCode.Unauthorized);
            unauthorizedResponse.Headers.Add("Content-Type", "application/json");
            await unauthorizedResponse.WriteStringAsync("{\"error\":\"Unauthorized\"}");
            context.GetInvocationResult().Value = unauthorizedResponse;
            return;
        }

        await next(context);
    }
}
