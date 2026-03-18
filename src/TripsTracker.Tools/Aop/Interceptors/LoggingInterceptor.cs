using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using TripsTracker.Tools.Aop.Attributes;

namespace TripsTracker.Tools.Aop.Interceptors;

/// <summary>
/// AOP interceptor that logs method entry, exit, parameters, and execution time.
/// Activated by <see cref="LogAttribute"/> on a method or class.
/// </summary>
public class LoggingInterceptor : IInterceptor
{
    private readonly ILoggerFactory _loggerFactory;

    public LoggingInterceptor(ILoggerFactory loggerFactory)
    {
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

        var logger = _loggerFactory.CreateLogger(invocation.TargetType);
        var methodName = $"{invocation.TargetType.Name}.{invocation.Method.Name}";
        var stopwatch = Stopwatch.StartNew();

        if (attribute.LogParameters && invocation.Arguments.Length > 0)
        {
            try
            {
                var parameters = JsonSerializer.Serialize(invocation.Arguments);
                logger.LogInformation("Calling {Method} with parameters {Parameters}", methodName, parameters);
            }
            catch
            {
                logger.LogInformation("Calling {Method}", methodName);
            }
        }
        else
        {
            logger.LogInformation("Calling {Method}", methodName);
        }

        try
        {
            invocation.Proceed();
            stopwatch.Stop();
            logger.LogInformation("Completed {Method} in {ElapsedMs}ms", methodName, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Failed {Method} after {ElapsedMs}ms", methodName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static LogAttribute? GetAttribute(IInvocation invocation)
        => invocation.Method.GetCustomAttributes(typeof(LogAttribute), true).FirstOrDefault() as LogAttribute
           ?? invocation.TargetType.GetCustomAttributes(typeof(LogAttribute), true).FirstOrDefault() as LogAttribute;
}
