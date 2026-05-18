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
    public async Task GeocodeAsync_Itacuruca_ReturnsPhotonCanonicalNameWithCoordinates()
    {
        // Regression: Nominatim uses the spelling "Itacurussa" while Photon uses "Itacuruça".
        // With Photon as primary geocoder the result must use Photon's canonical spelling
        // and carry valid coordinates — this is the city-name mismatch that previously
        // caused Add Place to fail when the user selected "Itacuruça" from autocomplete.
        var sut = BuildService();

        var result = await sut.GeocodeAsync("Itacuruça", "BR");

        Assert.IsNotNull(result, "Itacuruça must be found via Photon despite Nominatim spelling difference");
        Assert.IsTrue(result.Lat != 0 && result.Lon != 0, "Coordinates must be populated");
        Assert.IsTrue(result.City.Equals("Itacuruça", StringComparison.OrdinalIgnoreCase),
            $"City name must match Photon canonical spelling. Got: {result.City}");
        Assert.IsTrue(result.CountryIsoAlpha2.Equals("BR", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GeocodeAsync_SaoPaulo_PopulatesStateAbbr()
    {
        // Nominatim is still called for StateAbbr — verify it is populated for cities with known state codes.
        var sut = BuildService();

        var result = await sut.GeocodeAsync("São Paulo", "BR");

        Assert.IsNotNull(result);
        Assert.AreEqual("SP", result.StateAbbr,
            $"StateAbbr must be populated from Nominatim. Got: {result.StateAbbr}");
    }

    [TestMethod]
    public async Task GeocodeAsync_Gdansk_PopulatesStateAbbr()
    {
        // Poland uses numeric ISO 3166-2 codes (e.g. "22" for Pomeranian Voivodeship).
        // StateAbbr may be numeric — that is correct ISO data. Verify it is populated.
        var sut = BuildService();

        var result = await sut.GeocodeAsync("Gdansk", "PL");

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.StateAbbr, "StateAbbr must be populated for Gdansk");
    }

    [TestMethod]
    public async Task SuggestCitiesAsync_PartialPrefixWithCountry_ReturnsCityWithFullName()
    {
        // Regression: "Pinda" (prefix of "Pindamonhangaba") must return a result with country filter applied.
        var sut = BuildService();

        var results = await sut.SuggestCitiesAsync("Pinda", limit: 5, countryCode: "BR");

        Assert.IsTrue(
            results.Any(r => r.City.Equals("Pindamonhangaba", StringComparison.OrdinalIgnoreCase)),
            $"Expected 'Pindamonhangaba' in suggestions (BR filter). Got: [{string.Join(", ", results.Select(r => $"{r.City} ({r.CountryIsoAlpha2})"))}]");
    }
}
