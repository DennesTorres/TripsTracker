using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Tools.Registration;

namespace TripsTracker.Tests.Configuration;

[TestClass]
public class OptionsRegistrationTests
{
    #region Helpers

    private static IServiceProvider BuildProviderWithConfig(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddApplicationOptions(config);
        return services.BuildServiceProvider();
    }

    #endregion

    #region DatabaseOptions

    [TestMethod]
    public void DatabaseOptions_BindsConnectionStringFromConfiguration()
    {
        var provider = BuildProviderWithConfig(new()
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Server=test;Database=db;"
        });

        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        Assert.AreEqual("Server=test;Database=db;", options.ConnectionString);
    }

    [TestMethod]
    public void DatabaseOptions_IsRegisteredAsIOptions()
    {
        var provider = BuildProviderWithConfig(new()
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Server=test;"
        });

        var options = provider.GetService<IOptions<DatabaseOptions>>();

        Assert.IsNotNull(options);
    }

    #endregion

    #region ApplicationInsightsOptions

    [TestMethod]
    public void ApplicationInsightsOptions_BindsConnectionStringFromConfiguration()
    {
        var provider = BuildProviderWithConfig(new()
        {
            [$"{ApplicationInsightsOptions.SectionName}:ConnectionString"] = "InstrumentationKey=test;"
        });

        var options = provider.GetRequiredService<IOptions<ApplicationInsightsOptions>>().Value;

        Assert.AreEqual("InstrumentationKey=test;", options.ConnectionString);
    }

    [TestMethod]
    public void ApplicationInsightsOptions_IsRegisteredAsIOptions()
    {
        var provider = BuildProviderWithConfig(new()
        {
            [$"{ApplicationInsightsOptions.SectionName}:ConnectionString"] = "InstrumentationKey=test;"
        });

        var options = provider.GetService<IOptions<ApplicationInsightsOptions>>();

        Assert.IsNotNull(options);
    }

    #endregion
}
