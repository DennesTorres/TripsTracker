using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class CountryBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var dbOpts = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(dbOpts.ConnectionString)
            .Options;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CountryBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private readonly List<int> _trackedCountryIds = [];

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
            Biz = new CountryBusiness(Ctx, new FakeUserContext(1));
        }

        public async Task<Country> AddCountryAsync(Country country)
        {
            Ctx.Set<Country>().Add(country);
            await Ctx.SaveChangesAsync();
            _trackedCountryIds.Add(country.Id);
            return country;
        }

        public async ValueTask DisposeAsync()
        {
            if (_trackedCountryIds.Count > 0)
                await Ctx.Set<Country>()
                    .Where(c => _trackedCountryIds.Contains(c.Id))
                    .ExecuteDeleteAsync();
            await Ctx.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    // Use IsoAlpha2 codes outside the real ISO 3166-1 range to avoid conflicts with seeded data.
    // Real numeric codes are 004–894; these produce values > 8000.
    private static Country CountryWithIso3(string iso2, string iso3) => new()
    {
        IsoNumeric = (int)iso2[0] * 100 + (int)iso2[1],
        IsoAlpha2 = iso2,
        IsoAlpha3 = iso3,
        Flag = "🏳",
        Name = $"Test Country {iso2}",
        Region = "Test",
    };

    private static Country CountryWithoutIso3(string iso2) => new()
    {
        IsoNumeric = (int)iso2[0] * 100 + (int)iso2[1],
        IsoAlpha2 = iso2,
        IsoAlpha3 = null,
        Flag = "🏳",
        Name = $"Test Country {iso2}",
        Region = "Test",
    };

    #endregion

    [TestMethod]
    public async Task SetVisitedAsync_SetsIsVisited_ForCurrentUser()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("Y1"));

        var result = await f.Biz.SetVisitedAsync(country.Id, true);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsVisited);
    }

    [TestMethod]
    public async Task SetVisitedAsync_ClearsIsVisited_WhenSetToFalse()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("Y2"));
        await f.Biz.SetVisitedAsync(country.Id, true);

        var result = await f.Biz.SetVisitedAsync(country.Id, false);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsVisited);
    }

    [TestMethod]
    public async Task SetHomeAsync_SetsHomeAndVisited_ForCountry()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("Y3"));

        var result = await f.Biz.SetHomeAsync(country.Id, true);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsHome);
        Assert.IsTrue(result.IsVisited);
    }

    [TestMethod]
    public async Task SetHomeAsync_ClearsOtherHomesForUser()
    {
        await using var f = new Fixture();
        var c1 = await f.AddCountryAsync(CountryWithoutIso3("Y4"));
        var c2 = await f.AddCountryAsync(CountryWithoutIso3("Y5"));
        await f.Biz.SetHomeAsync(c1.Id, true);

        await f.Biz.SetHomeAsync(c2.Id, true);

        var uc1 = await f.Ctx.Set<TripsTracker.Data.Entities.UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == 1 && uc.CountryId == c1.Id);
        Assert.IsTrue(uc1 == null || !uc1.IsHome);
    }

    [TestMethod]
    public async Task SetShowStateBordersAsync_SetsFlag_ForCurrentUser()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("Y6"));

        var result = await f.Biz.SetShowStateBordersAsync(country.Id, true);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ShowStateBorders);
    }

    [TestMethod]
    public async Task SetShowStateBordersAsync_ClearsFlag_WhenSetToFalse()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("Y7"));
        await f.Biz.SetShowStateBordersAsync(country.Id, true);

        var result = await f.Biz.SetShowStateBordersAsync(country.Id, false);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.ShowStateBorders);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsIsoAlpha3_WhenCountryExists()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithIso3("X1", "XT1"));

        var result = await f.Biz.GetIsoAlpha3Async(country.Id);

        Assert.AreEqual("XT1", result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenIsoAlpha3NotSet()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync(CountryWithoutIso3("X2"));

        var result = await f.Biz.GetIsoAlpha3Async(country.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenCountryNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetIsoAlpha3Async(999_999);

        Assert.IsNull(result);
    }
}
