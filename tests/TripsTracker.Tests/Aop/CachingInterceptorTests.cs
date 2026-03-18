using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Memory;
using TripsTracker.Tools.Aop.Attributes;
using TripsTracker.Tools.Aop.Interceptors;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class CachingInterceptorTests
{
    private static readonly ProxyGenerator Generator = new();

    #region Test Helpers

    public interface ITestService
    {
        string GetValue();
        [Cache(DurationSeconds = 60)] string CachedGetValue();
        [Cache(DurationSeconds = 60)] string CachedWithArg(string arg);
        [Cache(DurationSeconds = 60, KeyPrefix = "custom")] string CachedWithPrefix(string arg);
        [Cache(DurationSeconds = 60)] string? NullReturningMethod();
    }

    private class TestService : ITestService
    {
        public int CallCount;

        public string GetValue() { CallCount++; return "value"; }
        public string CachedGetValue() { CallCount++; return "cached_value"; }
        public string CachedWithArg(string arg) { CallCount++; return $"value_{arg}"; }
        public string CachedWithPrefix(string arg) { CallCount++; return $"prefixed_{arg}"; }
        public string? NullReturningMethod() { CallCount++; return null; }
    }

    private static (CachingInterceptor interceptor, IMemoryCache cache) CreateInterceptor()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new CachingInterceptor(cache), cache);
    }

    #endregion

    [TestMethod]
    public void WithoutAttribute_MethodCalledEveryTime()
    {
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        proxy.GetValue();
        proxy.GetValue();

        Assert.AreEqual(2, impl.CallCount, "Non-cached method should execute on each call.");
    }

    [TestMethod]
    public void WithAttribute_SecondCallReturnsCachedValue()
    {
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        var first = proxy.CachedGetValue();
        var second = proxy.CachedGetValue();

        Assert.AreEqual(1, impl.CallCount, "Method should only execute once when cached.");
        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void WithAttribute_DifferentArgs_CachedSeparately()
    {
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        proxy.CachedWithArg("a");
        proxy.CachedWithArg("b");
        proxy.CachedWithArg("a"); // should be cached

        Assert.AreEqual(2, impl.CallCount, "Different args should produce separate cache entries.");
    }

    [TestMethod]
    public void WithAttribute_SameArg_ReturnsCachedOnRepeat()
    {
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        proxy.CachedWithArg("x");
        proxy.CachedWithArg("x");

        Assert.AreEqual(1, impl.CallCount);
    }

    [TestMethod]
    public void WithKeyPrefix_UsesCustomPrefix()
    {
        // Verifies that custom key prefix doesn't break caching behavior
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        proxy.CachedWithPrefix("z");
        proxy.CachedWithPrefix("z");

        Assert.AreEqual(1, impl.CallCount, "Custom prefix cached method should cache correctly.");
    }

    [TestMethod]
    public void WithAttribute_NullReturn_IsNotCached()
    {
        var impl = new TestService();
        var (interceptor, _) = CreateInterceptor();
        var proxy = Generator.CreateInterfaceProxyWithTarget<ITestService>(impl, interceptor);

        proxy.NullReturningMethod();
        proxy.NullReturningMethod();

        Assert.AreEqual(2, impl.CallCount, "Null return values should not be cached.");
    }
}
