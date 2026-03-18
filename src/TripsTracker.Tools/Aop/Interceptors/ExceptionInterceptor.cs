using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that enriches exceptions with context and tracks them to Application Insights.
/// Applied globally to all proxied classes — no attribute required.
/// Catches, enriches, and rethrows. HTTP mapping is handled by Functions middleware.
/// </summary>
public class ExceptionInterceptor : IInterceptor
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILoggerFactory _loggerFactory;

    public ExceptionInterceptor(TelemetryClient telemetryClient, ILoggerFactory loggerFactory)
    {
        _telemetryClient = telemetryClient;
        _loggerFactory = loggerFactory;
    }

    public void Intercept(IInvocation invocation)
    {
        try
        {
            invocation.Proceed();
        }
        catch (Exception ex)
        {
            var methodName = $"{invocation.TargetType.Name}.{invocation.Method.Name}";
            var logger = _loggerFactory.CreateLogger(invocation.TargetType);

            logger.LogError(ex, "Exception in {Method}", methodName);

            var telemetry = new ExceptionTelemetry(ex);
            telemetry.Properties["Method"] = methodName;
            telemetry.Properties["ExceptionType"] = ex.GetType().Name;
            _telemetryClient.TrackException(telemetry);

            throw;
        }
    }
}
