namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Thrown when the caller is not authorized to perform the operation.
/// Maps to HTTP 403.
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "You are not authorized to perform this action.")
        : base(message, "UNAUTHORIZED")
    {
    }
}
