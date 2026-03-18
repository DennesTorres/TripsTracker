namespace TripsTracker.Tools.Aop.Attributes;

/// <summary>
/// Marks a method for AOP logging interception.
/// Logs method entry, exit, parameters, and execution time.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class LogAttribute : Attribute
{
    /// <summary>
    /// Whether to log method parameters. Default: true.
    /// Set to false for methods with sensitive data.
    /// </summary>
    public bool LogParameters { get; set; } = true;
}
