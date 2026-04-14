using TripsTracker.Interfaces;

namespace TripsTracker.Functions.Middleware;

/// <summary>
/// Scoped request-level user context. Populated by JwtValidationMiddleware.
/// Registered as both IUserContext (for business class injection) and UserContext (for middleware population).
/// </summary>
public class UserContext : IUserContext
{
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public bool IsAuthenticated => UserId is not null;
}
