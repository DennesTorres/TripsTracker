using Castle.DynamicProxy;
using Microsoft.Extensions.Logging.Abstractions;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class ExceptionInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public interface ITestService
    {
        void Execute();
        void ThrowingMethod();
        void ThrowingWithAppException();
    }

    private class TestService : ITestService
    {
        public bool Executed;
        public void Execute() => Executed = true;
        public void ThrowingMethod() => throw new InvalidOperationException("boom");
        public void ThrowingWithAppException() => throw new TripsTracker.Interfaces.Exceptions.NotFoundException("Item", 1);
    }

    private static ExceptionInterceptor CreateInterceptor()
        => new(TelemetryClientFactory.NoOp(), NullLoggerFactory.Instance);

    #endregion

    [TestMethod]
    public void NoException_MethodProceeds()
    {
        var impl = new TestService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.Execute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WhenMethodThrows_ExceptionIsRethrown()
    {
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            new TestService(), CreateInterceptor());

        Assert.ThrowsExactly<InvalidOperationException>(() => proxy.ThrowingMethod());
    }

    [TestMethod]
    public void WhenAppExceptionThrown_OriginalTypeIsPreserved()
    {
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(
            new TestService(), CreateInterceptor());

        Assert.ThrowsExactly<TripsTracker.Interfaces.Exceptions.NotFoundException>(
            () => proxy.ThrowingWithAppException());
    }
}
