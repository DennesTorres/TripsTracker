using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Integration;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
public class CountriesProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _isoNumericSeed = 90000;

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

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TripsTrackerDbContext _ctx;
        private readonly CountryBusiness _countries;
        private readonly List<int> _countryIds = [];

        public Fixture()
        {
            _ctx = new TripsTrackerDbContext(_options);
            _countries = new CountryBusiness(_ctx, new FakeUserContext(1));
        }

        public async Task<Country> AddCountryAsync(string? isoAlpha3)
        {
            var isoNum = Interlocked.Increment(ref _isoNumericSeed);
            var country = new Country
            {
                IsoNumeric = isoNum,
                IsoAlpha2 = $"Z{(isoNum - 90000) % 10}",
                IsoAlpha3 = isoAlpha3,
                Flag = "🏳",
                Name = $"CountriesTest {isoNum}",
                Region = "Test",
            };
            _ctx.Set<Country>().Add(country);
            await _ctx.SaveChangesAsync();
            _countryIds.Add(country.Id);
            return country;
        }

        public CountriesProcess Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            var http = new HttpClient(new FakeHandler(respond));
            var geoBoundaries = new GeoBoundariesService(http);
            return new CountriesProcess(_countries, geoBoundaries);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var id in _countryIds)
                await _ctx.Set<Country>().Where(c => c.Id == id).ExecuteDeleteAsync();
            await _ctx.DisposeAsync();
        }
    }

    private const string SampleGeoJson = """{"type":"FeatureCollection","features":[]}""";
    private const string FakeDownloadUrl = "http://fake.geoboundaries/borders.json";
    private static string MetadataJson => $$"""{"gjDownloadURL":"{{FakeDownloadUrl}}"}""";

    #endregion

    [TestMethod]
    public async Task GetBordersAsync_ReturnsGeoJson_WhenCountryHasIsoAlpha3()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync("TST");

        var sut = f.Build(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("TST"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(MetadataJson, System.Text.Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(SampleGeoJson, System.Text.Encoding.UTF8, "application/json") };
        });

        var result = await sut.GetBordersAsync(country.Id);

        Assert.AreEqual(SampleGeoJson, result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenCountryHasNoIsoAlpha3()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(null);

        var sut = f.Build(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenGeoBoundariesReturnsNull()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync("ZZT");

        var sut = f.Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetBordersAsync(country.Id);

        Assert.IsNull(result);
    }
}
