using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using TripsTracker.Integration;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Integration;

[TestClass]
public class NominatimGeocodingServiceTests
{
    private static NominatimGeocodingService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var options = config.GetSection(NominatimOptions.SectionName).Get<NominatimOptions>()
            ?? throw new InvalidOperationException($"'{NominatimOptions.SectionName}' configuration section is missing.");

        var context = new ValidationContext(options);
        Validator.ValidateObject(options, context, validateAllProperties: true);

        var client = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl),
            DefaultRequestHeaders = { { "User-Agent", options.UserAgent } },
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };

        return new NominatimGeocodingService(client);
    }

    [TestMethod]
    public async Task GeocodeAsync_SaoPaulo_ReturnsBrazilResult()
    {
        var sut = BuildService();

        var result = await sut.GeocodeAsync("São Paulo", "BR");

        Assert.IsNotNull(result);
        Assert.AreEqual("BR", result.CountryIsoAlpha2);
        Assert.IsFalse(string.IsNullOrEmpty(result.City));
        Assert.IsTrue(result.Lat != 0 && result.Lon != 0);
    }

    [TestMethod]
    public async Task GeocodeAsync_RioDeJaneiro_ReturnsBrazilResult()
    {
        var sut = BuildService();

        var result = await sut.GeocodeAsync("Rio de Janeiro", "BR");

        Assert.IsNotNull(result);
        Assert.AreEqual("BR", result.CountryIsoAlpha2);
        Assert.IsFalse(string.IsNullOrEmpty(result.City));
    }

    [TestMethod]
    public async Task GeocodeAsync_NonExistentCity_ReturnsNull()
    {
        var sut = BuildService();

        var result = await sut.GeocodeAsync("ZZZNonExistentCityXXX", "XX");

        Assert.IsNull(result);
    }
}
