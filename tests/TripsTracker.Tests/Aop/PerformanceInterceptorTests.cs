using Castle.DynamicProxy;
using Microsoft.Extensions.Logging.Abstractions;
using TripsTracker.Tools.Aop.Attributes;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class PerformanceInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public interface ITestService
    {
        void Execute();
        [TrackPerformance] void MonitoredExecute();
        [TrackPerformance(ThresholdMilliseconds = 0)] void AlwaysSlowExecute();
    }

    private class TestService : ITestService
    {
        public bool Executed;
        public void Execute() => Executed = true;
        public void MonitoredExecute() => Executed = true;
        public void AlwaysSlowExecute() => Executed = true;
    }

    private static PerformanceInterceptor CreateInterceptor()
        => new(TelemetryClientFactory.NoOp(), NullLoggerFactory.Instance);

    #endregion

    [TestMethod]
    public void WithoutAttribute_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.Execute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_UnderThreshold_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.MonitoredExecute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithAttribute_ThresholdOfZero_MethodStillCompletes()
    {
        // Threshold of 0ms means the method will always be flagged as slow.
        // Verifies that slow-path telemetry/logging doesn't break execution.
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.AlwaysSlowExecute();

        Assert.IsTrue(impl.Executed);
    }
}
