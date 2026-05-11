using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
[DoNotParallelize]
public class ExploreBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _countryId;
    private static int _user1Id;
    private static int _user2Id;
    private static int _user3Id;
    private static int _place1Id; // CityA, user1
    private static int _place2Id; // CityA, user2 — same city, different user
    private static int _place3Id; // CityB, user3

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
            "Database=TripsTracker_Test_Explore");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var country = new Country { IsoNumeric = 9010, IsoAlpha2 = "EX", Flag = "🏳", Name = "ExploreTestCountry", Region = "Test" };
        ctx.Countries.Add(country);
        var u1 = new User { Email = "seed1@explore.test", CreatedAt = DateTime.UtcNow };
        var u2 = new User { Email = "seed2@explore.test", CreatedAt = DateTime.UtcNow };
        var u3 = new User { Email = "seed3@explore.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync();

        var p1 = new Place { City = "CityA", StateName = "StateA", CountryId = country.Id, UserId = u1.Id, Lat = 10, Lon = 20 };
        var p2 = new Place { City = "CityA", StateName = "StateA", CountryId = country.Id, UserId = u2.Id, Lat = 10.1, Lon = 20.1 };
        var p3 = new Place { City = "CityB", CountryId = country.Id, UserId = u3.Id, Lat = -5, Lon = 30 };
        ctx.Places.AddRange(p1, p2, p3);
        await ctx.SaveChangesAsync();

        _countryId = country.Id;
        _user1Id = u1.Id;
        _user2Id = u2.Id;
        _user3Id = u3.Id;
        _place1Id = p1.Id;
        _place2Id = p2.Id;
        _place3Id = p3.Id;
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

        public ExploreBusiness Business() => new(Ctx);

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    [TestMethod]
    public async Task SearchAsync_ReturnsEmpty_WhenQueryMatchesNoCity()
    {
        await using var f = new Fixture();
        var results = await f.Business().SearchAsync("XYZNonexistent");
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_ReturnsLocation_WithUserCount()
    {
        await using var f = new Fixture();
        var results = await f.Business().SearchAsync("CityB");
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("CityB", results[0].City);
        Assert.AreEqual(1, results[0].UserCount);
    }

    [TestMethod]
    public async Task SearchAsync_GroupsSameCityAcrossMultipleUsers()
    {
        await using var f = new Fixture();
        var results = await f.Business().SearchAsync("CityA");
        Assert.AreEqual(1, results.Count, "CityA from two users should be one grouped result");
        Assert.AreEqual(2, results[0].UserCount);
        Assert.AreEqual(_countryId, results[0].CountryId);
    }

    [TestMethod]
    public async Task SearchAsync_EmptyQuery_ReturnsAllLocations()
    {
        await using var f = new Fixture();
        var results = await f.Business().SearchAsync("");
        Assert.IsTrue(results.Count >= 2, "Empty query should return all locations");
        var cities = results.Select(r => r.City).ToList();
        CollectionAssert.Contains(cities, "CityA");
        CollectionAssert.Contains(cities, "CityB");
    }

    [TestMethod]
    public async Task GetContentAsync_ReturnsPhotosFromAllUsersWithSameCity()
    {
        await using var f = new Fixture();

        // Add a photo for each user's CityA place
        var photo1 = new PlacePhoto
        {
            PlaceId = _place1Id, UserId = _user1Id, BlobName = "b1", ContentType = "image/jpeg",
            SizeBytes = 100, SortOrder = 1, UploadedAt = DateTime.UtcNow,
        };
        var photo2 = new PlacePhoto
        {
            PlaceId = _place2Id, UserId = _user2Id, BlobName = "b2", ContentType = "image/jpeg",
            SizeBytes = 100, SortOrder = 1, UploadedAt = DateTime.UtcNow,
        };
        f.Ctx.Set<PlacePhoto>().AddRange(photo1, photo2);
        await f.Ctx.SaveChangesAsync();

        var content = await f.Business().GetContentAsync("CityA", _countryId);
        Assert.AreEqual(2, content.Photos.Count, "Should return photos from both users for CityA");
    }

    [TestMethod]
    public async Task GetContentAsync_ReturnsCommentsFromAllUsersWithSameCity()
    {
        await using var f = new Fixture();

        var c1 = new PlaceComment { PlaceId = _place1Id, UserId = _user1Id, Text = "Hello from user1", CreatedAt = DateTime.UtcNow };
        var c2 = new PlaceComment { PlaceId = _place2Id, UserId = _user2Id, Text = "Hello from user2", CreatedAt = DateTime.UtcNow };
        f.Ctx.Set<PlaceComment>().AddRange(c1, c2);
        await f.Ctx.SaveChangesAsync();

        var content = await f.Business().GetContentAsync("CityA", _countryId);
        Assert.AreEqual(2, content.Comments.Count, "Should return comments from both users for CityA");
    }

    [TestMethod]
    public async Task GetContentAsync_DoesNotReturnContentFromDifferentCity()
    {
        await using var f = new Fixture();

        var photo = new PlacePhoto
        {
            PlaceId = _place3Id, UserId = _user3Id, BlobName = "b3", ContentType = "image/jpeg",
            SizeBytes = 100, SortOrder = 1, UploadedAt = DateTime.UtcNow,
        };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var content = await f.Business().GetContentAsync("CityA", _countryId);
        Assert.AreEqual(0, content.Photos.Count, "CityA content should not include CityB photos");
    }
}
