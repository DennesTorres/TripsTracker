namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// Maps to HTTP 404.
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.", "NOT_FOUND")
    {
    }

    public NotFoundException(string message)
        : base(message, "NOT_FOUND")
    {
    }
}
