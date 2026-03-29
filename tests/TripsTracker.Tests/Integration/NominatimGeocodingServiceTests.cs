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

    [TestMethod]
    public async Task SuggestCitiesAsync_PartialPrefix_ReturnsCityWithFullName()
    {
        // Regression: "Pinda" (prefix of "Pindamonhangaba") must return a result
        // without requiring the full name to be typed.
        var sut = BuildService();

        var results = await sut.SuggestCitiesAsync("Pinda", limit: 5);

        Assert.IsTrue(
            results.Any(r => r.City.Equals("Pindamonhangaba", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Pindamonhangaba' in suggestions. Got: [{string.Join(", ", results.Select(r => $"{r.City} ({r.CountryIsoAlpha2})"))}]");
    }

    [TestMethod]
    public async Task SuggestCitiesAsync_PartialPrefixWithCountry_ReturnsCityWithFullName()
    {
        // Same regression with country filter applied.
        var sut = BuildService();

        var results = await sut.SuggestCitiesAsync("Pinda", limit: 5, countryCode: "BR");

        Assert.IsTrue(
            results.Any(r => r.City.Equals("Pindamonhangaba", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Pindamonhangaba' in suggestions (BR filter). Got: [{string.Join(", ", results.Select(r => $"{r.City} ({r.CountryIsoAlpha2})"))}]");
    }

    [TestMethod]
    public async Task SuggestCitiesAsync_ReturnsCoordinatesForEachSuggestion()
    {
        // Regression: suggestions must carry Lat/Lon from Photon GeoJSON geometry so
        // PlacesProcess can skip Nominatim re-geocoding when the user selects one.
        // If geometry reading is broken, Lat/Lon are null and the bypass never fires.
        var sut = BuildService();

        var results = await sut.SuggestCitiesAsync("São Paulo", limit: 5, countryCode: "BR");

        Assert.IsNotEmpty(results, "Expected at least one suggestion for 'São Paulo'");
        foreach (var r in results)
        {
            Assert.IsNotNull(r.Lat,  $"Lat must not be null for suggestion '{r.City}'");
            Assert.IsNotNull(r.Lon,  $"Lon must not be null for suggestion '{r.City}'");
            Assert.IsTrue(r.Lat >= -90  && r.Lat <= 90,  $"Lat {r.Lat} out of range for '{r.City}'");
            Assert.IsTrue(r.Lon >= -180 && r.Lon <= 180, $"Lon {r.Lon} out of range for '{r.City}'");
        }
    }
}
