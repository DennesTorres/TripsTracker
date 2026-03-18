using Castle.DynamicProxy;
using Microsoft.Extensions.Logging.Abstractions;
using TripsTracker.Tools.Aop.Attributes;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class LoggingInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public interface ITestService
    {
        void Execute();
        [Log] void LoggedExecute();
        [Log] void LoggedWithArg(string arg);
        [Log] void LoggedThrowing();
    }

    [Log]
    private class LoggedService : ITestService
    {
        public bool Executed;
        public void Execute() => Executed = true;
        public void LoggedExecute() => Executed = true;
        public void LoggedWithArg(string arg) => Executed = true;
        public void LoggedThrowing() => throw new InvalidOperationException("test error");
    }

    private static LoggingInterceptor CreateInterceptor()
        => new(NullLoggerFactory.Instance);

    #endregion

    [TestMethod]
    public void WithoutAttribute_MethodProceeds()
    {
        var impl = new LoggedService();
        // Proxy against an interface that has no [Log] on Execute()
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.Execute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithMethodAttribute_MethodProceeds()
    {
        var impl = new LoggedService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        // Should not throw
        proxy.LoggedExecute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithMethodAttribute_AndArgument_MethodProceeds()
    {
        var impl = new LoggedService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.LoggedWithArg("some value");

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WithClassAttribute_MethodProceeds()
    {
        var impl = new LoggedService();
        // LoggedService has [Log] on the class — interceptor reads TargetType attribute
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        proxy.Execute();

        Assert.IsTrue(impl.Executed);
    }

    [TestMethod]
    public void WhenMethodThrows_ExceptionIsRethrown()
    {
        var impl = new LoggedService();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, CreateInterceptor());

        Assert.ThrowsExactly<InvalidOperationException>(() => proxy.LoggedThrowing());
    }
}
