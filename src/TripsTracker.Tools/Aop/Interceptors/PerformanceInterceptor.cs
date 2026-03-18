using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TripsTracker.Tools.Aop.Attributes;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that monitors method execution time.
/// Flags executions exceeding the configured threshold to Application Insights.
/// Activated by <see cref="TrackPerformanceAttribute"/> on a method or class.
/// </summary>
public class PerformanceInterceptor : IInterceptor
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILoggerFactory _loggerFactory;

    public PerformanceInterceptor(TelemetryClient telemetryClient, ILoggerFactory loggerFactory)
    {
        _telemetryClient = telemetryClient;
        _loggerFactory = loggerFactory;
    }

    public void Intercept(IInvocation invocation)
    {
        var attribute = GetAttribute(invocation);
        if (attribute is null)
        {
            invocation.Proceed();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        invocation.Proceed();
        stopwatch.Stop();

        var methodName = $"{invocation.TargetType.Name}.{invocation.Method.Name}";
        var elapsed = stopwatch.ElapsedMilliseconds;

        if (elapsed > attribute.ThresholdMilliseconds)
        {
            var logger = _loggerFactory.CreateLogger(invocation.TargetType);
            logger.LogWarning("Slow method detected: {Method} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                methodName, elapsed, attribute.ThresholdMilliseconds);

            var telemetry = new EventTelemetry("SlowMethod");
            telemetry.Properties["Method"] = methodName;
            telemetry.Properties["ElapsedMs"] = elapsed.ToString();
            telemetry.Properties["ThresholdMs"] = attribute.ThresholdMilliseconds.ToString();
            _telemetryClient.TrackEvent(telemetry);
        }
    }

    private static TrackPerformanceAttribute? GetAttribute(IInvocation invocation)
        => invocation.Method.GetCustomAttributes(typeof(TrackPerformanceAttribute), true).FirstOrDefault() as TrackPerformanceAttribute
           ?? invocation.TargetType.GetCustomAttributes(typeof(TrackPerformanceAttribute), true).FirstOrDefault() as TrackPerformanceAttribute;
}
