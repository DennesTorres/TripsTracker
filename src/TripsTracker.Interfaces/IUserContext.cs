namespace TripsTracker.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user within the request scope.
/// Populated by JwtValidationMiddleware before any business class is invoked.
/// </summary>
public interface IUserContext
{
    /// <summary>Internal integer user ID. Null when request is unauthenticated.</summary>
    int? UserId { get; }
    /// <summary>Email address used as login identifier. Null when unauthenticated.</summary>
    string? Email { get; }
    bool IsAuthenticated { get; }
}
