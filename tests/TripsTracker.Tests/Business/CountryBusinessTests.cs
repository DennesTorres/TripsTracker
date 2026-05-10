using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class CountryBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var dbOpts = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        var connStr = System.Text.RegularExpressions.Regex.Replace(
            dbOpts.ConnectionString,
            @"(?i)(database|initial\s+catalog)\s*=\s*[^;]+",
            "Database=TripsTracker_Test_Countries");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CountryBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private IDbContextTransaction? _transaction;

        public Fixture(int userId = 1)
        {
            Ctx = new TripsTrackerDbContext(_options);
            var userContext = new FakeUserContext(userId);
            Biz = new CountryBusiness(Ctx, userContext);
        }

        public async Task BeginTransactionAsync()
            => _transaction = await Ctx.Database.BeginTransactionAsync();

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
            }
            await Ctx.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private static Country CountryWithIso3(string iso2, string iso3) => new()
    {
        IsoNumeric = (int)iso2[0] * 100 + (int)iso2[1],
        IsoAlpha2 = iso2,
        IsoAlpha3 = iso3,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Europe",
    };

    private static Country CountryWithoutIso3(string iso2) => new()
    {
        IsoNumeric = (int)iso2[0] * 100 + (int)iso2[1],
        IsoAlpha2 = iso2,
        IsoAlpha3 = null,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Europe",
    };

    #endregion

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsIsoAlpha3_WhenCountryExists()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = CountryWithIso3("DE", "DEU");
        f.Ctx.Set<Country>().Add(country);
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetIsoAlpha3Async(country.Id);

        Assert.AreEqual("DEU", result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenIsoAlpha3NotSet()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = CountryWithoutIso3("XX");
        f.Ctx.Set<Country>().Add(country);
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetIsoAlpha3Async(country.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenCountryNotFound()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();

        var result = await f.Biz.GetIsoAlpha3Async(999);

        Assert.IsNull(result);
    }
}
