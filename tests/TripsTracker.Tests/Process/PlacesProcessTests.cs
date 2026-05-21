using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Integration;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

file sealed class TestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public TestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

file sealed class FakeGeocodingHandler : HttpMessageHandler
{
    // Returns canned Photon JSON for any Photon request; empty array for Nominatim fallback.
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri?.ToString() ?? "";
        if (url.Contains("photon.komoot.io"))
        {
            const string json = """{"features":[{"properties":{"name":"São Paulo","osm_value":"city","countrycode":"br","country":"Brazil","state":"São Paulo"},"geometry":{"coordinates":[-46.63,-23.55]}}]}""";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
        // Nominatim fallback — empty result; StateAbbr will be null (non-fatal)
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        });
    }
}

[TestClass]
[DoNotParallelize]
public class PlacesProcessTests
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
            "Database=TripsTracker_Test_PlacesProcess5");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

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
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Places WHERE UserId IN (1, 2)");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM UserCountries WHERE UserId IN (1, 2)");
    }

    private TripsTrackerDbContext CreateCtx() => new(_options);

    private static PlacesProcess CreateSut(TripsTrackerDbContext ctx, int userId)
    {
        var userCtx = new TestUserContext(userId);
        var places = new PlaceBusiness(ctx, userCtx);
        var countries = new CountryBusiness(ctx, userCtx);
        var http = new HttpClient(new FakeGeocodingHandler())
        {
            BaseAddress = new Uri("https://nominatim.openstreetmap.org")
        };
        var geocoding = new GeocodingBusiness(new NominatimGeocodingService(http));
        return new PlacesProcess(places, countries, geocoding);
    }

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

    #endregion

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_NonHomePlace_ReturnsPromptFalse()
    {
        await using var ctx = CreateCtx();
        var place = await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: false);

        var sut = CreateSut(ctx, userId: 1);
        var result = await sut.DeleteAsync(place.Id);

        Assert.IsFalse(result.PromptHomeCountry);
        Assert.IsNull(result.CountryId);
        Assert.IsNull(result.CountryName);
    }

    [TestMethod]
    public async Task DeleteAsync_HomePlace_OtherHomeRemains_ReturnsPromptFalse()
    {
        await using var ctx = CreateCtx();
        // Insert two home places directly (bypassing business layer) so both have IsHome=true
        await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: true);
        var place2 = await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: true);

        var sut = CreateSut(ctx, userId: 1);
        var result = await sut.DeleteAsync(place2.Id);

        Assert.IsFalse(result.PromptHomeCountry);
    }

    [TestMethod]
    public async Task DeleteAsync_HomePlace_NoOtherHome_ReturnsPromptTrue_WithCountryInfo()
    {
        await using var ctx = CreateCtx();
        var place = await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: true);

        var sut = CreateSut(ctx, userId: 1);
        var result = await sut.DeleteAsync(place.Id);

        Assert.IsTrue(result.PromptHomeCountry);
        Assert.AreEqual(_brazilId, result.CountryId);
        Assert.AreEqual("Brazil", result.CountryName);
    }

    [TestMethod]
    public async Task DeleteAsync_LastPlaceInCountry_UnsetsVisited()
    {
        await using var ctx = CreateCtx();
        ctx.Set<UserCountry>().Add(new UserCountry { UserId = 1, CountryId = _brazilId, IsVisited = true });
        await ctx.SaveChangesAsync();
        var place = await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: false);

        var sut = CreateSut(ctx, userId: 1);
        await sut.DeleteAsync(place.Id);

        await using var verifyCtx = new TripsTrackerDbContext(_options);
        var uc = await verifyCtx.Set<UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == 1 && uc.CountryId == _brazilId);
        Assert.IsFalse(uc?.IsVisited ?? false, "Country should no longer be marked as visited after last place is deleted.");
    }

    // ─── AddAsync ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_StoresCityNameFromGeocodingResult()
    {
        // City name stored must come from the geocoding result (Photon canonical name), not from the DTO.
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx, userId: 1);

        var result = await sut.AddAsync(new AddPlaceDto("São Paulo", "BR"));

        Assert.AreEqual("São Paulo", result.City, "City name must come from geocoding result.");
    }

    [TestMethod]
    public async Task AddAsync_SetsCountryAsVisited()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx, userId: 1);

        await sut.AddAsync(new AddPlaceDto("São Paulo", "BR", IsHome: false));

        await using var verifyCtx = new TripsTrackerDbContext(_options);
        var uc = await verifyCtx.Set<UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == 1 && uc.CountryId == _brazilId);
        Assert.IsTrue(uc?.IsVisited ?? false, "Country should be marked as visited after adding a place.");
    }

    [TestMethod]
    public async Task AddAsync_WhenIsHomeTrue_SetsCountryAsHome()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx, userId: 1);

        await sut.AddAsync(new AddPlaceDto("São Paulo", "BR", IsHome: true));

        await using var verifyCtx = new TripsTrackerDbContext(_options);
        var uc = await verifyCtx.Set<UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == 1 && uc.CountryId == _brazilId);
        Assert.IsTrue(uc?.IsHome ?? false, "Country should be marked as home after adding a home place.");
    }

    // ─── UpdateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_WhenIsHomeTrue_SetsCountryHomeFlag()
    {
        await using var ctx = CreateCtx();
        var place = await InsertPlaceAsync(ctx, _brazilId, userId: 1, isHome: false);

        var sut = CreateSut(ctx, userId: 1);
        await sut.UpdateAsync(place.Id, new UpdatePlaceDto(IsHome: true));

        await using var verifyCtx = new TripsTrackerDbContext(_options);
        var uc = await verifyCtx.Set<UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == 1 && uc.CountryId == _brazilId);
        Assert.IsTrue(uc?.IsHome ?? false, "Country home flag should be set after updating place to IsHome=true.");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenNotFound_ReturnsNull()
    {
        await using var ctx = CreateCtx();
        var sut = CreateSut(ctx, userId: 1);

        var result = await sut.UpdateAsync(999999, new UpdatePlaceDto(IsHome: true));

        Assert.IsNull(result, "UpdateAsync should return null when the place does not exist.");
    }
}
