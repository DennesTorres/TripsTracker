using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

file sealed class PublicMapTestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public PublicMapTestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
[DoNotParallelize]
public class PublicMapProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _brazilId;

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
            "Database=TripsTracker_Test_PublicMapProcess5");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        // EnsureCreatedAsync does not run migrations — create VIEW manually
        await ctx.Database.ExecuteSqlRawAsync("""
            CREATE VIEW VisitedStates AS
            SELECT
                CAST(ROW_NUMBER() OVER (ORDER BY p.UserId, p.CountryId, p.StateAbbr) AS int) AS Id,
                p.UserId,
                p.CountryId,
                p.StateAbbr,
                p.StateName
            FROM (
                SELECT UserId, CountryId, StateAbbr, MAX(StateName) AS StateName
                FROM Places
                WHERE StateAbbr IS NOT NULL
                GROUP BY UserId, CountryId, StateAbbr
            ) AS p;
            """);

        await ctx.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT Users ON;
            INSERT INTO Users (Id, Email, CreatedAt, IsDiscoverable)
            VALUES (1, 'user1@test.com', GETUTCDATE(), 0),
                   (2, 'user2@test.com', GETUTCDATE(), 0);
            SET IDENTITY_INSERT Users OFF;");

        var brazil = new Country { IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "🇧🇷", Name = "Brazil", Region = "Americas" };
        ctx.Set<Country>().Add(brazil);
        await ctx.SaveChangesAsync();
        _brazilId = brazil.Id;
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM ShareLinks WHERE UserId IN (1, 2)");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Places WHERE UserId IN (1, 2)");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM UserCountries WHERE UserId IN (1, 2)");
    }

    private TripsTrackerDbContext CreateCtx() => new(_options);

    private static PublicMapProcess CreateSut(TripsTrackerDbContext ctx, int userId)
    {
        var userCtx = new PublicMapTestUserContext(userId);
        var shareLinks = new ShareLinkBusiness(ctx, userCtx);
        var places = new PlaceBusiness(ctx, userCtx);
        var countries = new CountryBusiness(ctx, userCtx);
        var states = new VisitedStateBusiness(ctx, userCtx);
        var users = new UserBusiness(ctx);
        return new PublicMapProcess(shareLinks, places, countries, states, users);
    }

    private static async Task<ShareLink> InsertShareLinkAsync(TripsTrackerDbContext ctx, int userId, bool isActive = true, DateTime? expiresAt = null)
    {
        var link = new ShareLink
        {
            UserId = userId,
            Token = Guid.NewGuid().ToString("N")[..32],
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            ViewCount = 0
        };
        ctx.Set<ShareLink>().Add(link);
        await ctx.SaveChangesAsync();
        return link;
    }

    private static async Task<Place> InsertPlaceAsync(TripsTrackerDbContext ctx, int countryId, int userId)
    {
        var place = new Place
        {
            Lon = -46.63, Lat = -23.55, CountryId = countryId,
            City = "São Paulo", StateAbbr = "SP", StateName = "São Paulo",
            IsHome = false, UserId = userId
        };
        ctx.Set<Place>().Add(place);
        await ctx.SaveChangesAsync();
        return place;
    }

    #endregion

    // ─── GetSharedMapAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetSharedMapAsync_TokenNotFound_ReturnsNull()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx, userId: 1);

        var result = await sut.GetSharedMapAsync("nonexistent-token-xyz");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSharedMapAsync_InactiveToken_ReturnsNull()
    {
        await using var ctx = CreateCtx();
        var link = await InsertShareLinkAsync(ctx, userId: 1, isActive: false);

        var sut = CreateSut(ctx, userId: 2); // viewer
        var result = await sut.GetSharedMapAsync(link.Token);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSharedMapAsync_ValidToken_IncrementsViewCount()
    {
        await using var ctx = CreateCtx();
        var link = await InsertShareLinkAsync(ctx, userId: 1);

        var sut = CreateSut(ctx, userId: 2); // viewer
        await sut.GetSharedMapAsync(link.Token);

        await using var verifyCtx = new TripsTrackerDbContext(_options);
        var updated = await verifyCtx.Set<ShareLink>().FindAsync(link.Id);
        Assert.AreEqual(1, updated!.ViewCount, "ViewCount should be incremented after a valid map view.");
    }

    [TestMethod]
    public async Task GetSharedMapAsync_ValidToken_ReturnsOwnerData()
    {
        await using var ctx = CreateCtx();
        var link = await InsertShareLinkAsync(ctx, userId: 1);
        await InsertPlaceAsync(ctx, _brazilId, userId: 1);
        ctx.Set<UserCountry>().Add(new UserCountry { UserId = 1, CountryId = _brazilId, IsVisited = true });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx, userId: 2); // viewer
        var result = await sut.GetSharedMapAsync(link.Token);

        Assert.IsNotNull(result);
        Assert.AreEqual("user1@test.com", result.OwnerDisplayName, "OwnerDisplayName should fall back to owner's email when DisplayName is null.");
        Assert.IsNotEmpty(result.Places, "Owner's places should be returned.");
        Assert.IsNotEmpty(result.Countries, "Owner's countries should be returned.");
    }

    [TestMethod]
    public async Task GetSharedMapAsync_OtherUsersData_NotIncluded()
    {
        await using var ctx = CreateCtx();
        var link = await InsertShareLinkAsync(ctx, userId: 1); // owner = user 1
        await InsertPlaceAsync(ctx, _brazilId, userId: 2);     // user 2 has a place, user 1 has none

        var sut = CreateSut(ctx, userId: 2);
        var result = await sut.GetSharedMapAsync(link.Token);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Places, "Only owner's places should be returned — not other users' places.");
    }
}
