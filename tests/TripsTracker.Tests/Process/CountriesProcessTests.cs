using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Integration;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
[DoNotParallelize]
public class CountriesProcessTests
{
    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static BlobServiceClient _blobClient = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var dbOpts = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(dbOpts.ConnectionString)
            .Options;

        _blobClient = new BlobServiceClient("UseDevelopmentStorage=true");

        // Clean up any leftover test countries from prior failed runs
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Set<Country>().Where(c => c.IsoNumeric >= 90000).ExecuteDeleteAsync();
    }

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }

    private const string SampleGeoJson = """{"type":"FeatureCollection","features":[]}""";
    private const string MetadataJson = """{"gjDownloadURL":"https://cdn.example.com/test.geojson"}""";

    private static int _isoNumericSeed = 90000;

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly List<int> _countryIds = [];
        private readonly List<string> _blobNames = [];

        public Fixture() => Ctx = new TripsTrackerDbContext(_options);

        public CountriesProcess Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            var countries = new CountryBusiness(Ctx, new FakeUserContext(1));
            var http = new HttpClient(new FakeHandler(respond));
            var geoBoundaries = new GeoBoundariesService(http);
            var borderCache = new BlobBorderCacheService(_blobClient);
            return new CountriesProcess(countries, geoBoundaries, borderCache, NullLogger<CountriesProcess>.Instance);
        }

        public async Task<Country> AddCountryAsync(string? isoAlpha3)
        {
            var country = new Country
            {
                IsoNumeric = Interlocked.Increment(ref _isoNumericSeed),
                IsoAlpha2 = "XX",
                IsoAlpha3 = isoAlpha3,
                Flag = "🏳",
                Name = $"Test-{Guid.NewGuid():N}",
                Region = "Test",
            };
            Ctx.Set<Country>().Add(country);
            await Ctx.SaveChangesAsync();
            _countryIds.Add(country.Id);
            return country;
        }

        public void TrackBlob(string blobName) => _blobNames.Add(blobName);

        public async ValueTask DisposeAsync()
        {
            var container = _blobClient.GetBlobContainerClient("borders");
            foreach (var name in _blobNames)
                await container.GetBlobClient(name).DeleteIfExistsAsync();

            if (_countryIds.Count > 0)
                await Ctx.Set<Country>().Where(c => _countryIds.Contains(c.Id)).ExecuteDeleteAsync();
            await Ctx.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsGeoJson_WhenCountryHasIsoAlpha3()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync("XA1");
        f.TrackBlob("XA1.json");
        var sut = f.Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("geoboundaries.org")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MetadataJson) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleGeoJson) });

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("FeatureCollection"));
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenCountryHasNoIsoAlpha3()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(null);
        var sut = f.Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenGeoBoundariesReturnsNull()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync("XA3");
        var sut = f.Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsCachedGeoJson_WhenCacheHit()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync("XA4");
        f.TrackBlob("XA4.json");

        // Pre-populate the cache
        var cache = new BlobBorderCacheService(_blobClient);
        await cache.SetAsync("XA4", SampleGeoJson);

        // GeoBoundaries handler should not be called
        var geoBoundariesCalled = false;
        var sut = f.Build(_ => { geoBoundariesCalled = true; return new HttpResponseMessage(HttpStatusCode.NotFound); });

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("FeatureCollection"));
        Assert.IsFalse(geoBoundariesCalled, "GeoBoundaries must not be called when cache is populated");
    }

    [TestMethod]
    public async Task GetBordersAsync_SimplifiesPolygon_WhenRingHasCollinearPoints()
    {
        // Ring has 6 points; (0.5,0) is collinear with (0,0) and (1,0).
        // RDP simplification (epsilon=0.01) should remove it → output ring has 5 points.
        const string collinearGeoJson = """
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"shapeISO":"XR1-01","shapeName":"Test Region"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[0.5,0],[1,0],[1,1],[0,1],[0,0]]]}}]}
            """;

        await using var f = new Fixture();
        var country = await f.AddCountryAsync("XR1");
        f.TrackBlob("XR1.json");
        var sut = f.Build(req =>
            req.RequestUri!.AbsoluteUri.Contains("geoboundaries.org")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MetadataJson) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(collinearGeoJson) });

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNotNull(result);
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        var ring = doc.RootElement
            .GetProperty("features")[0]
            .GetProperty("geometry")
            .GetProperty("coordinates")[0];

        Assert.IsTrue(ring.GetArrayLength() < 6,
            $"Expected RDP to reduce collinear ring below 6 points, got {ring.GetArrayLength()}");
    }
}
