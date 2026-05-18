using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
[DoNotParallelize]
public class PlaceBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _countryAId;
    private static int _countryBId;
    private static int _user1Id;
    private static int _user2Id;

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
            "Database=TripsTracker_Test_PlaceBusiness");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var cA = new Country { IsoNumeric = 9020, IsoAlpha2 = "PA", Flag = "🏳", Name = "PlaceBizCountryA", Region = "RegionAlpha" };
        var cB = new Country { IsoNumeric = 9021, IsoAlpha2 = "PB", Flag = "🏳", Name = "PlaceBizCountryB", Region = "RegionBeta" };
        ctx.Countries.AddRange(cA, cB);
        var u1 = new User { Email = "u1@placebiz.test", CreatedAt = DateTime.UtcNow };
        var u2 = new User { Email = "u2@placebiz.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        _countryAId = cA.Id;
        _countryBId = cB.Id;
        _user1Id = u1.Id;
        _user2Id = u2.Id;
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

        public PlaceBusiness ForUser(int userId)
        {
            return new PlaceBusiness(Ctx, new TestUserContext(userId));
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    private static Place SeedPlace(TripsTrackerDbContext ctx, int userId, int countryId, string city, bool isHome = false)
    {
        var place = new Place { UserId = userId, CountryId = countryId, City = city, Lat = 1, Lon = 1, IsHome = isHome };
        ctx.Places.Add(place);
        ctx.SaveChanges();
        return place;
    }

    #endregion

    // ── HasAnyForCurrentUserInRegionAsync ────────────────────────────────────

    [TestMethod]
    public async Task HasAnyForCurrentUserInRegionAsync_ReturnsFalse_WhenNoPlacesInRegion()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).HasAnyForCurrentUserInRegionAsync("RegionAlpha");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasAnyForCurrentUserInRegionAsync_ReturnsTrue_WhenUserHasPlaceInRegion()
    {
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user1Id, _countryAId, "CityInAlpha");
        var result = await f.ForUser(_user1Id).HasAnyForCurrentUserInRegionAsync("RegionAlpha");
        Assert.IsTrue(result);
    }

    // ── HasAnyGloballyInCityAsync ────────────────────────────────────────────

    [TestMethod]
    public async Task HasAnyGloballyInCityAsync_ReturnsFalse_WhenNoOneHasVisitedCity()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).HasAnyGloballyInCityAsync("UnvisitedCity", _countryAId);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasAnyGloballyInCityAsync_ReturnsTrue_WhenAnotherUserHasVisitedCity()
    {
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user2Id, _countryAId, "SharedCity");
        var result = await f.ForUser(_user1Id).HasAnyGloballyInCityAsync("SharedCity", _countryAId);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasAnyGloballyInCityAsync_ReturnsFalse_WhenCityExistsInDifferentCountry()
    {
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user2Id, _countryBId, "CityOnlyInB");
        var result = await f.ForUser(_user1Id).HasAnyGloballyInCityAsync("CityOnlyInB", _countryAId);
        Assert.IsFalse(result);
    }

    // ── HasAnyGloballyInCountryAsync ─────────────────────────────────────────

    [TestMethod]
    public async Task HasAnyGloballyInCountryAsync_ReturnsFalse_WhenNoOneInCountry()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).HasAnyGloballyInCountryAsync(_countryBId);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasAnyGloballyInCountryAsync_ReturnsTrue_WhenAnotherUserInCountry()
    {
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user2Id, _countryBId, "SomeCityInB");
        var result = await f.ForUser(_user1Id).HasAnyGloballyInCountryAsync(_countryBId);
        Assert.IsTrue(result);
    }

    // ── HasAnyGloballyInRegionAsync ──────────────────────────────────────────

    [TestMethod]
    public async Task HasAnyGloballyInRegionAsync_ReturnsFalse_WhenNoOneInRegion()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).HasAnyGloballyInRegionAsync("RegionBeta");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasAnyGloballyInRegionAsync_ReturnsTrue_WhenAnotherUserInRegion()
    {
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user2Id, _countryBId, "CityForRegionBeta");
        var result = await f.ForUser(_user1Id).HasAnyGloballyInRegionAsync("RegionBeta");
        Assert.IsTrue(result);
    }

    // ── GetFirstForCurrentUserInCountryAsync ─────────────────────────────────

    [TestMethod]
    public async Task GetFirstForCurrentUserInCountryAsync_ReturnsNull_WhenNoPlaces()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).GetFirstForCurrentUserInCountryAsync(_countryAId);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFirstForCurrentUserInCountryAsync_ReturnsEarliestPlace()
    {
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _countryAId, "FirstCityA");
        SeedPlace(f.Ctx, _user1Id, _countryAId, "SecondCityA");
        var result = await f.ForUser(_user1Id).GetFirstForCurrentUserInCountryAsync(_countryAId);
        Assert.IsNotNull(result);
        Assert.AreEqual(p1.Id, result.Id);
    }

    // ── GetFirstForCurrentUserInRegionAsync ──────────────────────────────────

    [TestMethod]
    public async Task GetFirstForCurrentUserInRegionAsync_ReturnsNull_WhenNoPlaces()
    {
        await using var f = new Fixture();
        var result = await f.ForUser(_user1Id).GetFirstForCurrentUserInRegionAsync("RegionAlpha");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetFirstForCurrentUserInRegionAsync_ReturnsEarliestPlace()
    {
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _countryAId, "FirstAlpha");
        SeedPlace(f.Ctx, _user1Id, _countryAId, "SecondAlpha");
        var result = await f.ForUser(_user1Id).GetFirstForCurrentUserInRegionAsync("RegionAlpha");
        Assert.IsNotNull(result);
        Assert.AreEqual(p1.Id, result.Id);
    }

    // ── HOME_EXCLUSIVITY ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_WhenIsHomeTrue_ClearsOtherHomePlaces()
    {
        await using var f = new Fixture();
        var existing = SeedPlace(f.Ctx, _user1Id, _countryAId, "OldHome", isHome: true);
        var sut = f.ForUser(_user1Id);

        await sut.CreateAsync(new CreatePlaceDto(1, 1, _countryBId, "NewHome", null, null, true));

        f.Ctx.ChangeTracker.Clear();
        var oldPlace = await f.Ctx.Places.AsNoTracking().FirstAsync(p => p.Id == existing.Id);
        Assert.IsFalse(oldPlace.IsHome, "Old home place must have IsHome cleared when a new home place is created");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenIsHomeTrue_ClearsOtherHomePlaces()
    {
        await using var f = new Fixture();
        var existing = SeedPlace(f.Ctx, _user1Id, _countryAId, "OldHome", isHome: true);
        var target = SeedPlace(f.Ctx, _user1Id, _countryBId, "Target");
        var sut = f.ForUser(_user1Id);

        await sut.UpdateAsync(target.Id, new UpdatePlaceDto("Target", true));

        f.Ctx.ChangeTracker.Clear();
        var oldPlace = await f.Ctx.Places.AsNoTracking().FirstAsync(p => p.Id == existing.Id);
        Assert.IsFalse(oldPlace.IsHome, "Old home place must have IsHome cleared when another place is updated to IsHome=true");
    }
}
