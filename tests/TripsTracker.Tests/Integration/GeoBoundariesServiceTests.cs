using System.Net;
using TripsTracker.Integration;

namespace TripsTracker.Tests.Integration;

[TestClass]
public class GeoBoundariesServiceTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(handler(request));
    }

    private const string SampleGeoJson = """{"type":"FeatureCollection","features":[]}""";
    private const string MetadataJson = """{"gjDownloadURL":"https://cdn.example.com/DEU_ADM1.geojson"}""";

    private static GeoBoundariesService BuildService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var client = new HttpClient(new FakeHandler(handler));
        return new GeoBoundariesService(client);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnGeoJson_WhenCountryFound()
    {
        var sut = BuildService(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("geoboundaries.org"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(MetadataJson) };
            return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(SampleGeoJson) };
        });

        var result = await sut.GetBordersAsync("DEU");

        Assert.IsNotNull(result);
        Assert.AreEqual(SampleGeoJson, result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenMetadataEndpointReturns404()
    {
        var sut = BuildService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetBordersAsync("ZZZ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenDownloadUrlMissing()
    {
        var sut = BuildService(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{}") });

        var result = await sut.GetBordersAsync("DEU");

        Assert.IsNull(result);
    }
}
