namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Thrown when input validation fails.
/// Maps to HTTP 400.
/// </summary>
public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public ValidationException(string field, string error)
        : base("One or more validation errors occurred.", "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = [error]
        };
    }
}
