namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Base class for all application exceptions.
/// Carries an optional error code for client-facing responses.
/// </summary>
public abstract class AppException : Exception
{
    public string ErrorCode { get; }

    protected AppException(string message, string errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
