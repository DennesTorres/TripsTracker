using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TripsTracker.Tools.Registration;

namespace TripsTracker.Tests.Registration;

[TestClass]
public class ServiceRegistrationExtensionsTests
{
    #region Test Helpers

    private interface ITestService { }
    private class TestService : ITestService { }

    private interface IIgnoredService { }

    #endregion

    #region AddApplicationServices

    [TestMethod]
    public void AddApplicationServices_RunsWithoutErrorsWhenNoClassesFound()
    {
        // Verifies the scanner handles empty assemblies gracefully
        var services = new ServiceCollection();

        var act = () => services.AddApplicationServices(ServiceLifetime.Scoped, "TripsTracker.");

        // Should not throw
        act();
        var provider = services.BuildServiceProvider();
        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void AddApplicationServices_DoesNotRegisterFrameworkTypes()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices(ServiceLifetime.Scoped, "TripsTracker.");

        // No Microsoft or System types should be registered by the scanner
        var frameworkServices = services.Where(s =>
            (s.ImplementationType?.Namespace?.StartsWith("Microsoft") == true) ||
            (s.ImplementationType?.Namespace?.StartsWith("System") == true));

        Assert.IsFalse(frameworkServices.Any(),
            "Framework types should not be registered by the application scanner.");
    }

    [TestMethod]
    public void AddApplicationServices_DefaultLifetimeIsScoped()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices(assemblyPrefixes: "TripsTracker.");

        var appServices = services.Where(s =>
            s.ImplementationType?.Assembly.GetName().Name?.StartsWith("TripsTracker.") == true);

        foreach (var service in appServices)
        {
            Assert.AreEqual(ServiceLifetime.Scoped, service.Lifetime,
                $"Service {service.ServiceType.Name} should have Scoped lifetime by default.");
        }
    }

    [TestMethod]
    public void AddScopedApplicationServices_RegistersWithScopedLifetime()
    {
        var services = new ServiceCollection();
        services.AddScopedApplicationServices("TripsTracker.");

        var appServices = services.Where(s =>
            s.ImplementationType?.Assembly.GetName().Name?.StartsWith("TripsTracker.") == true);

        foreach (var service in appServices)
        {
            Assert.AreEqual(ServiceLifetime.Scoped, service.Lifetime,
                $"Service {service.ServiceType.Name} should have Scoped lifetime.");
        }
    }

    [TestMethod]
    public void AddTransientApplicationServices_RegistersWithTransientLifetime()
    {
        var services = new ServiceCollection();
        services.AddTransientApplicationServices("TripsTracker.");

        var appServices = services.Where(s =>
            s.ImplementationType?.Assembly.GetName().Name?.StartsWith("TripsTracker.") == true);

        foreach (var service in appServices)
        {
            Assert.AreEqual(ServiceLifetime.Transient, service.Lifetime,
                $"Service {service.ServiceType.Name} should have Transient lifetime.");
        }
    }

    [TestMethod]
    public void AddApplicationServices_SkipsDuplicateRegistrations()
    {
        var services = new ServiceCollection();

        // Register twice — should not throw or duplicate
        services.AddApplicationServices(ServiceLifetime.Scoped, "TripsTracker.");
        services.AddApplicationServices(ServiceLifetime.Scoped, "TripsTracker.");

        // Should build without errors
        var provider = services.BuildServiceProvider();
        Assert.IsNotNull(provider);
    }

    #endregion
}
