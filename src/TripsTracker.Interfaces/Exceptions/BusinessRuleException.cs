namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Thrown when a business rule is violated.
/// Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string errorCode = "BUSINESS_RULE_VIOLATION")
        : base(message, errorCode)
    {
    }
}
