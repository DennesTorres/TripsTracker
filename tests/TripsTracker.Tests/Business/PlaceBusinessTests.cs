using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

file sealed class TestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public TestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
public class PlaceBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _countryId;

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

        // Seed users with explicit IDs (required for FK on Places)
        await ctx.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT Users ON;
            INSERT INTO Users (Id, Email, CreatedAt, IsDiscoverable)
            VALUES (1, 'user1@test.com', GETUTCDATE(), 0),
                   (2, 'user2@test.com', GETUTCDATE(), 0),
                   (3, 'user3@test.com', GETUTCDATE(), 0);
            SET IDENTITY_INSERT Users OFF;");

        // Seed a country for use across tests
        var country = new Country
        {
            IsoNumeric = 9901, IsoAlpha2 = "T1", Flag = "🏳",
            Name = "TestCountry", Region = "Test"
        };
        ctx.Set<Country>().Add(country);
        await ctx.SaveChangesAsync();
        _countryId = country.Id;
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        // Each test uses userIds 1–3 in dedicated places; clean up after every test
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Places WHERE UserId IN (1, 2, 3)");
    }

    private TripsTrackerDbContext CreateCtx() => new(_options);

    private static async Task<Place> InsertPlaceAsync(TripsTrackerDbContext ctx, int countryId, int userId, bool isHome)
    {
        var place = new Place
        {
            Lon = 0, Lat = 0, CountryId = countryId,
            City = "City", StateAbbr = "ST", StateName = "State",
            IsHome = isHome, UserId = userId
        };
        ctx.Set<Place>().Add(place);
        await ctx.SaveChangesAsync();
        return place;
    }

    private static PlaceBusiness CreateSut(TripsTrackerDbContext ctx, int userId) =>
        new(ctx, new TestUserContext(userId));

    #endregion

    // ─── HOME_EXCLUSIVITY ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_WhenIsHomeTrue_ClearsExistingHomePlace()
    {
        await using var ctx = CreateCtx();
        var existing = await InsertPlaceAsync(ctx, _countryId, userId: 1, isHome: true);

        var sut = CreateSut(ctx, userId: 1);
        await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "NewCity", "ST", "State", IsHome: true));

        await ctx.Entry(existing).ReloadAsync();
        Assert.IsFalse(existing.IsHome, "Existing home place should be cleared when new home is created.");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenIsHomeTrue_ClearsExistingHomePlace()
    {
        await using var ctx = CreateCtx();
        var homePlace = await InsertPlaceAsync(ctx, _countryId, userId: 2, isHome: true);
        var otherPlace = await InsertPlaceAsync(ctx, _countryId, userId: 2, isHome: false);

        var sut = CreateSut(ctx, userId: 2);
        await sut.UpdateAsync(otherPlace.Id, new UpdatePlaceDto(IsHome: true));

        await ctx.Entry(homePlace).ReloadAsync();
        Assert.IsFalse(homePlace.IsHome, "Existing home place should be cleared when another place is updated to IsHome=true.");
    }

    [TestMethod]
    public async Task UpdateAsync_City_CannotBeChanged()
    {
        await using var ctx = CreateCtx();
        var place = await InsertPlaceAsync(ctx, _countryId, userId: 3, isHome: false);

        var sut = CreateSut(ctx, userId: 3);
        // UpdatePlaceDto no longer accepts City — compile-time enforcement
        // This test verifies the place City remains unchanged after update
        await sut.UpdateAsync(place.Id, new UpdatePlaceDto(IsHome: false));

        await ctx.Entry(place).ReloadAsync();
        Assert.AreEqual("City", place.City, "City should not change on update.");
    }
}
