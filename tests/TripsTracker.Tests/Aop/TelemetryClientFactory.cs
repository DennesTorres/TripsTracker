using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Extensions.DependencyInjection;

namespace TripsTracker.Tests.Aop;

internal static class TelemetryClientFactory
{
    private static readonly TelemetryClient _instance = Create();
    public static TelemetryClient NoOp() => _instance;

    private static TelemetryClient Create()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationInsightsTelemetryWorkerService(
            (ApplicationInsightsServiceOptions options) =>
            {
                options.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
            });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<TelemetryClient>();
    }
}
