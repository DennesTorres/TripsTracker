using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Tests.Business;

file sealed class TestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email => $"user{UserId}@test.com";
    public bool IsAuthenticated => UserId is not null;
    public TestUserContext(int userId) { UserId = userId; }
}

[TestClass]
[DoNotParallelize]
public class PlaceBusinessTests
{
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

    [TestCleanup]
    public async Task TestCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Places WHERE UserId = {0}", _userId);
    }

    private TripsTrackerDbContext CreateCtx() => new(_options);
    private PlaceBusiness CreateSut(TripsTrackerDbContext ctx) => new(ctx, new TestUserContext(_userId));

    // ─── CreateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_ThrowsBusinessRuleException_WhenDuplicateCityExists()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx);
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
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx);
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

    [TestMethod]
    public async Task CreateAsync_WhenIsHomeTrue_ClearsExistingHomePlace()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx);
        var existing = await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "OldHome", null, null, IsHome: true));

        await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "NewHome", null, null, IsHome: true));

        var reloaded = await ctx.Set<Place>().FindAsync(existing.Id);
        await ctx.Entry(reloaded!).ReloadAsync();
        Assert.IsFalse(reloaded!.IsHome, "Existing home place must be cleared when a new home is created.");
    }

    // ─── UpdateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_WhenIsHomeTrue_ClearsExistingHomePlace()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx);
        var existing = await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "HomeA", null, null, IsHome: true));
        var other = await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "HomeB", null, null, IsHome: false));

        await sut.UpdateAsync(other.Id, new UpdatePlaceDto(IsHome: true));

        var reloaded = await ctx.Set<Place>().FindAsync(existing.Id);
        await ctx.Entry(reloaded!).ReloadAsync();
        Assert.IsFalse(reloaded!.IsHome, "Previous home place must be cleared when another place is set as home.");
    }

    [TestMethod]
    public async Task UpdateAsync_City_CannotBeChanged()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx);
        var place = await sut.CreateAsync(new CreatePlaceDto(0, 0, _countryId, "OriginalCity", null, null, IsHome: false));

        await sut.UpdateAsync(place.Id, new UpdatePlaceDto(IsHome: false));

        var reloaded = await ctx.Set<Place>().FindAsync(place.Id);
        await ctx.Entry(reloaded!).ReloadAsync();
        Assert.AreEqual("OriginalCity", reloaded!.City, "City must be immutable — UpdatePlaceDto must not carry City.");
    }
}
