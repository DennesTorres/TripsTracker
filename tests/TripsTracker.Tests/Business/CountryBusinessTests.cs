using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

file sealed class CountryTestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public CountryTestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
[DoNotParallelize]
public class CountryBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _countryId;
    private static int _userId;

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
            "Database=TripsTracker_Test_CountryBusiness");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var country = new Country { IsoNumeric = 9030, IsoAlpha2 = "CB", Flag = "🏳", Name = "CountryBizTest", Region = "TestRegion" };
        ctx.Countries.Add(country);
        var user = new User { Email = "u@countrybiz.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        _countryId = country.Id;
        _userId = user.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
        }

        public CountryBusiness ForUser(int userId)
            => new CountryBusiness(Ctx, new CountryTestUserContext(userId));

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    [TestMethod]
    public async Task GetByIdAsync_ReturnsCountry_WhenIdExists()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_userId).GetByIdAsync(_countryId);
        Assert.IsNotNull(result);
        Assert.AreEqual(_countryId, result.Id);
        Assert.AreEqual("CountryBizTest", result.Name);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_userId).GetByIdAsync(int.MaxValue);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsIsHome_False_WhenNoUserCountryRow()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_userId).GetByIdAsync(_countryId);
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsHome);
        Assert.IsFalse(result.IsVisited);
    }
}
