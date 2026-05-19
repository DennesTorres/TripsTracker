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

    [TestMethod]
    public async Task GetBordersAsync_RemapsProperties_ShapeIsoToISO1_ShapeNameToNAME1()
    {
        const string rawGeoJson = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"shapeISO":"DE-BY","shapeName":"Bavaria","shapeArea":1.0},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}}]}
            """;

        var sut = BuildService(req =>
            req.RequestUri!.AbsoluteUri.Contains("geoboundaries.org")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MetadataJson) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rawGeoJson) });

        var result = await sut.GetBordersAsync("DEU");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("\"ISO_1\""), "shapeISO should be remapped to ISO_1");
        Assert.IsTrue(result.Contains("\"NAME_1\""), "shapeName should be remapped to NAME_1");
        Assert.IsFalse(result.Contains("\"shapeISO\""), "Original shapeISO should be removed");
        Assert.IsFalse(result.Contains("\"shapeName\""), "Original shapeName should be removed");
    }

    [TestMethod]
    public async Task GetBordersAsync_RewindsPolygonRings_ReversingCoordinateOrder()
    {
        // CW ring (geoBoundaries format): [0,0] → [1,0] → [1,1] → [0,1] → [0,0]
        // After rewind (CCW):             [0,0] → [0,1] → [1,1] → [1,0] → [0,0]
        const string rawGeoJson = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"shapeISO":"X","shapeName":"Y"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}}]}
            """;

        var sut = BuildService(req =>
            req.RequestUri!.AbsoluteUri.Contains("geoboundaries.org")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MetadataJson) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(rawGeoJson) });

        var result = await sut.GetBordersAsync("DEU");

        Assert.IsNotNull(result);
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        var ring = doc.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates")[0];

        // ring[1] should be [0,1] (lon=0, lat=1) — CCW order
        // Before rewind it would be [1,0] (lon=1, lat=0) — CW order
        Assert.AreEqual(0.0, ring[1][0].GetDouble(), "After rewind: ring[1] lon should be 0");
        Assert.AreEqual(1.0, ring[1][1].GetDouble(), "After rewind: ring[1] lat should be 1");
    }
}
