using System.Net;
using System.Net.Http.Json;
using System.Text;
using Moq;
using Moq.Protected;
using TripsTracker.Integration;

namespace TripsTracker.Tests.Integration;

[TestClass]
public class NominatimGeocodingServiceTests
{
    private static HttpClient BuildHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json"),
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org"),
        };
        return client;
    }

    [TestMethod]
    public async Task GeocodeAsync_ValidCity_ReturnsParsedResult()
    {
        const string json = """
        [{
            "lat": "-23.5557714",
            "lon": "-46.6395571",
            "address": {
                "city": "São Paulo",
                "state_code": "SP",
                "country_code": "br"
            }
        }]
        """;

        var sut = new NominatimGeocodingService(BuildHttpClient(json));

        var result = await sut.GeocodeAsync("São Paulo", "BR");

        Assert.IsNotNull(result);
        Assert.AreEqual("São Paulo", result.City);
        Assert.AreEqual("SP", result.StateAbbr);
        Assert.AreEqual("BR", result.CountryIsoAlpha2);
        Assert.AreEqual(-23.5557714, result.Lat, 0.0001);
        Assert.AreEqual(-46.6395571, result.Lon, 0.0001);
    }

    [TestMethod]
    public async Task GeocodeAsync_CityWithTownField_ReturnsTownAsCity()
    {
        const string json = """
        [{
            "lat": "51.5074",
            "lon": "-0.1278",
            "address": {
                "town": "Islington",
                "country_code": "gb"
            }
        }]
        """;

        var sut = new NominatimGeocodingService(BuildHttpClient(json));

        var result = await sut.GeocodeAsync("Islington", "GB");

        Assert.IsNotNull(result);
        Assert.AreEqual("Islington", result.City);
        Assert.IsNull(result.StateAbbr);
        Assert.AreEqual("GB", result.CountryIsoAlpha2);
    }

    [TestMethod]
    public async Task GeocodeAsync_EmptyResults_ReturnsNull()
    {
        var sut = new NominatimGeocodingService(BuildHttpClient("[]"));

        var result = await sut.GeocodeAsync("NonExistentCity", "XX");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GeocodeAsync_NoCityAddressField_FallsBackToCityNameParam()
    {
        const string json = """
        [{
            "lat": "40.7128",
            "lon": "-74.0060",
            "address": {
                "state_code": "NY",
                "country_code": "us"
            }
        }]
        """;

        var sut = new NominatimGeocodingService(BuildHttpClient(json));

        var result = await sut.GeocodeAsync("New York", "US");

        Assert.IsNotNull(result);
        Assert.AreEqual("New York", result.City);
    }
}
