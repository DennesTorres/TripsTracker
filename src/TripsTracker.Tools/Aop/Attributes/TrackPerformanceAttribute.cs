namespace TripsTracker.Tools.Aop.Attributes;

/// <summary>
/// Marks a method for AOP performance monitoring interception.
/// Flags executions exceeding the threshold to Application Insights.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class TrackPerformanceAttribute : Attribute
{
    /// <summary>
    /// Execution time threshold in milliseconds. Default: 1000ms.
    /// Executions exceeding this are flagged as slow in Application Insights.
    /// </summary>
    public int ThresholdMilliseconds { get; set; } = 1000;
}
