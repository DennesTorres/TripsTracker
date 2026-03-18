using Castle.DynamicProxy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TripsTracker.Tools.Aop.Interceptors;
using TripsTracker.Tools.Registration;

namespace TripsTracker.Tests.Aop;

[TestClass]
public class AopRegistrationTests
{
    [TestMethod]
    public void AddAopInfrastructure_RegistersProxyGeneratorAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddAopInfrastructure();

        var registration = services.FirstOrDefault(s => s.ServiceType == typeof(IProxyGenerator));

        Assert.IsNotNull(registration, "IProxyGenerator should be registered.");
        Assert.AreEqual(ServiceLifetime.Singleton, registration.Lifetime,
            "IProxyGenerator should be singleton.");
    }

    [TestMethod]
    public void AddAopInfrastructure_RegistersAllInterceptorsAsScoped()
    {
        var services = new ServiceCollection();
        services.AddAopInfrastructure();

        var interceptorTypes = new[]
        {
            typeof(LoggingInterceptor),
            typeof(ExceptionInterceptor),
            typeof(TransactionInterceptor),
            typeof(ValidationInterceptor),
            typeof(PerformanceInterceptor),
            typeof(CachingInterceptor)
        };

        foreach (var type in interceptorTypes)
        {
            var registration = services.FirstOrDefault(s => s.ServiceType == type);

            Assert.IsNotNull(registration, $"{type.Name} should be registered.");
            Assert.AreEqual(ServiceLifetime.Scoped, registration.Lifetime,
                $"{type.Name} should be Scoped.");
        }
    }

    [TestMethod]
    public void AddAopInfrastructure_RegistersMemoryCache()
    {
        var services = new ServiceCollection();
        services.AddAopInfrastructure();

        var registration = services.FirstOrDefault(s => s.ServiceType == typeof(IMemoryCache));

        Assert.IsNotNull(registration, "IMemoryCache should be registered for CachingInterceptor.");
    }

    [TestMethod]
    public void AddAopInfrastructure_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();

        // Should be idempotent
        services.AddAopInfrastructure();
        services.AddAopInfrastructure();

        Assert.IsNotNull(services);
    }
}
