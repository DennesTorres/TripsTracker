namespace TripsTracker.Tools.Aop.Attributes;

/// <summary>
/// Marks a method for AOP caching interception.
/// Caches the return value for the specified duration.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CacheAttribute : Attribute
{
    /// <summary>
    /// Cache duration in seconds. Default: 300 (5 minutes).
    /// </summary>
    public int DurationSeconds { get; set; } = 300;

    /// <summary>
    /// Optional cache key prefix. Defaults to the fully qualified method name.
    /// </summary>
    public string? KeyPrefix { get; set; }
}
