using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PlaceBusinessTests
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
            "Database=TripsTracker_Test_Places");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var country = new Country { IsoNumeric = 9002, IsoAlpha2 = "PL", Flag = "🏳", Name = "PlaceTestCountry", Region = "Test" };
        ctx.Countries.Add(country);
        var user = new User { Email = "seed@places.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        _countryId = country.Id;
        _userId = user.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly Mock<IUserContext> _userContextMock = new();
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public PlaceBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PlaceBusiness(Ctx, _userContextMock.Object);
        }

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose(); // no Complete() → automatic rollback
        }
    }

    #endregion

    // ─── CreateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_ThrowsBusinessRuleException_WhenDuplicateCityExists()
    {
        await using var f = new Fixture();
        var sut = f.ForUser(_userId);
        var dto = new CreatePlaceDto(Lon: 0, Lat: 0, CountryId: _countryId, City: "Dupeville",
            StateAbbr: null, StateName: null, IsHome: false);

        await sut.CreateAsync(dto);

        BusinessRuleException? ex = null;
        try { await sut.CreateAsync(dto); }
        catch (BusinessRuleException e) { ex = e; }

        Assert.IsNotNull(ex, "Expected BusinessRuleException was not thrown");
        Assert.AreEqual("DUPLICATE_PLACE", ex.ErrorCode);
    }

    [TestMethod]
    public async Task CreateAsync_IsCaseInsensitive_WhenCheckingForDuplicates()
    {
        await using var f = new Fixture();
        var sut = f.ForUser(_userId);
        var original = new CreatePlaceDto(Lon: 0, Lat: 0, CountryId: _countryId, City: "CaseCity",
            StateAbbr: null, StateName: null, IsHome: false);
        var lowerCase = new CreatePlaceDto(Lon: 0, Lat: 0, CountryId: _countryId, City: "casecity",
            StateAbbr: null, StateName: null, IsHome: false);

        await sut.CreateAsync(original);

        BusinessRuleException? ex = null;
        try { await sut.CreateAsync(lowerCase); }
        catch (BusinessRuleException e) { ex = e; }

        Assert.IsNotNull(ex, "Expected BusinessRuleException was not thrown");
        Assert.AreEqual("DUPLICATE_PLACE", ex.ErrorCode);
    }
}
