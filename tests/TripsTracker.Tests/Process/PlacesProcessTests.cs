using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Integration;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
[DoNotParallelize]
public class PlacesProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _brazilId;
    private static int _argentinaId;
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
            "Database=TripsTracker_Test_PlacesProcess7");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var brazil = new Country { IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "🇧🇷", Name = "Brazil", Region = "South America" };
        var argentina = new Country { IsoNumeric = 32, IsoAlpha2 = "AR", Flag = "🇦🇷", Name = "Argentina", Region = "South America" };
        var uk = new Country { IsoNumeric = 826, IsoAlpha2 = "GB", Flag = "🇬🇧", Name = "United Kingdom", Region = "Europe" };
        ctx.Countries.AddRange(brazil, argentina, uk);
        var u1 = new User { Email = "u1@process.test", CreatedAt = DateTime.UtcNow };
        var u2 = new User { Email = "u2@process.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        _brazilId = brazil.Id;
        _argentinaId = argentina.Id;
        _user1Id = u1.Id;
        _user2Id = u2.Id;
    }

    /// <summary>
    /// Returns canned geocoding responses for Photon and Nominatim without making real HTTP calls.
    /// Photon URL (photon.komoot.io): returns Itacuruça, Brazil at (-22.93, -43.90).
    /// Nominatim URL (/search): returns state abbreviation RJ.
    /// </summary>
    private sealed class FakeGeocodingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri?.ToString() ?? "";
            string json = url.Contains("photon.komoot.io")
                ? """{"features":[{"properties":{"name":"Itacuruça","osm_value":"city","countrycode":"BR","country":"Brazil","state":"Rio de Janeiro"},"geometry":{"coordinates":[-43.90,-22.93]}}]}"""
                : """[{"lat":"-22.93","lon":"-43.90","address":{"state_code":"RJ"}}]""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public PlacesProcess Sut { get; }
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);

            var userContext = new TestUserContext(_user1Id);

            var httpClient = new HttpClient(new FakeGeocodingHandler())
                { BaseAddress = new Uri("https://nominatim.openstreetmap.org") };
            var geocoding = new GeocodingBusiness(new NominatimGeocodingService(httpClient));

            var places = new PlaceBusiness(Ctx, userContext);
            var countries = new CountryBusiness(Ctx, userContext);
            var points = new PointsBusiness(Ctx, userContext);
            Sut = new PlacesProcess(places, countries, geocoding, points, userContext);
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

    private static void SeedPointEvent(TripsTrackerDbContext ctx, int userId, string eventType, int points, int? referenceId, string? referenceType)
    {
        var evt = new PointEvent { UserId = userId, EventType = eventType, Points = points, ReferenceId = referenceId, ReferenceType = referenceType, CreatedAt = DateTime.UtcNow };
        ctx.Set<PointEvent>().Add(evt);
        ctx.SaveChanges();
    }

    #endregion

    // ─── AddAsync — cascade scoring ──────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_AwardsOnlyCityAdded_WhenNotFirstInCountryOrRegion()
    {
        // user1 already in Brazil (not first in country) and Argentina (not first in region);
        // user2 has Itacuruça in Brazil (not first globally in city) → city_added 50 only
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user1Id, _brazilId, "Manaus");
        SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");
        SeedPlace(f.Ctx, _user2Id, _brazilId, "Itacuruça");

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(1, events.Count, "Only city_added expected");
        Assert.AreEqual("city_added", events[0].EventType);
        Assert.AreEqual(50, events[0].Points);
    }

    [TestMethod]
    public async Task AddAsync_AwardsCityPioneer_WhenFirstGloballyInCity()
    {
        // user1 already in Brazil and Argentina (no country/continent tiers);
        // no one has visited Itacuruça → city_pioneer 200
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user1Id, _brazilId, "Manaus");
        SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(1, events.Count, "Only city_pioneer expected");
        Assert.AreEqual("city_pioneer", events[0].EventType);
        Assert.AreEqual(200, events[0].Points);
    }

    [TestMethod]
    public async Task AddAsync_AwardsCountryFirst_WhenFirstPersonallyInCountry()
    {
        // user1 has Argentina (region covered); user2 has Itacuruça in Brazil (city+country not pioneer);
        // user1 adding first Brazil place → city_added 50 + country_first 500
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");
        SeedPlace(f.Ctx, _user2Id, _brazilId, "Itacuruça");

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(2, events.Count, "city_added + country_first expected");
        Assert.IsTrue(events.Any(e => e.EventType == "city_added" && e.Points == 50));
        Assert.IsTrue(events.Any(e => e.EventType == "country_first" && e.Points == 500));
    }

    [TestMethod]
    public async Task AddAsync_AwardsCountryPioneer_WhenFirstGloballyInCountry()
    {
        // user1 has Argentina (region not first); no Brazil places globally → city_pioneer + country_pioneer
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(2, events.Count, "city_pioneer + country_pioneer expected");
        Assert.IsTrue(events.Any(e => e.EventType == "city_pioneer" && e.Points == 200));
        Assert.IsTrue(events.Any(e => e.EventType == "country_pioneer" && e.Points == 2000));
    }

    [TestMethod]
    public async Task AddAsync_AwardsContinentFirst_WhenFirstPersonallyInRegion()
    {
        // user1 has no S.America places; user2 has Itacuruça in Brazil and Argentina
        // (city+country+continent not pioneer globally) → city_added 50 + country_first 500 + continent_first 5000
        await using var f = new Fixture();
        SeedPlace(f.Ctx, _user2Id, _brazilId, "Itacuruça");
        SeedPlace(f.Ctx, _user2Id, _argentinaId, "BuenosAires");

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(3, events.Count, "city_added + country_first + continent_first expected");
        Assert.IsTrue(events.Any(e => e.EventType == "city_added" && e.Points == 50));
        Assert.IsTrue(events.Any(e => e.EventType == "country_first" && e.Points == 500));
        Assert.IsTrue(events.Any(e => e.EventType == "continent_first" && e.Points == 5000));
    }

    [TestMethod]
    public async Task AddAsync_AwardsContinentPioneer_WhenFirstGloballyInRegion()
    {
        // no S.America places globally → all three pioneer tiers
        await using var f = new Fixture();

        await f.Sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        var events = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.Points > 0)
            .ToListAsync();
        Assert.AreEqual(3, events.Count, "city_pioneer + country_pioneer + continent_pioneer expected");
        Assert.IsTrue(events.Any(e => e.EventType == "city_pioneer" && e.Points == 200));
        Assert.IsTrue(events.Any(e => e.EventType == "country_pioneer" && e.Points == 2000));
        Assert.IsTrue(events.Any(e => e.EventType == "continent_pioneer" && e.Points == 20000));
    }

    // ─── DeleteAsync — cascade revocation ────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_RevokesPlacePoints_Always()
    {
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça");
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");

        await f.Sut.DeleteAsync(p1.Id);

        var revoked = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.EventType == "city_added_revoked" && e.Points == -50)
            .FirstOrDefaultAsync();
        Assert.IsNotNull(revoked, "city_added_revoked event expected");
    }

    [TestMethod]
    public async Task DeleteAsync_RevokesCountryPoints_WhenLastPlaceInCountry()
    {
        // user1 only has P1 in Brazil; Argentina place kept so region is not revoked
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça");
        SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");
        SeedPointEvent(f.Ctx, _user1Id, "country_first", 500, p1.Id, "Country");

        await f.Sut.DeleteAsync(p1.Id);

        var revoked = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.EventType == "country_first_revoked" && e.Points == -500)
            .FirstOrDefaultAsync();
        Assert.IsNotNull(revoked, "country_first_revoked event expected");
    }

    [TestMethod]
    public async Task DeleteAsync_ReassignsCountryPoints_WhenOtherPlacesInCountry()
    {
        // user1 has P1 and P2 in Brazil; deleting P1 → country points move to P2
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça");
        var p2 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Manaus");
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");
        SeedPointEvent(f.Ctx, _user1Id, "country_first", 500, p1.Id, "Country");

        await f.Sut.DeleteAsync(p1.Id);

        var reassigned = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.EventType == "country_first"
                     && e.Points == 500 && e.ReferenceId == p2.Id && e.ReferenceType == "Country")
            .FirstOrDefaultAsync();
        Assert.IsNotNull(reassigned, "country_first re-awarded on surviving place expected");
    }

    [TestMethod]
    public async Task DeleteAsync_RevokesRegionPoints_WhenLastPlaceInRegion()
    {
        // user1 only has P1 in S.America; deleting it → continent points revoked
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça");
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");
        SeedPointEvent(f.Ctx, _user1Id, "continent_first", 5000, p1.Id, "Continent");

        await f.Sut.DeleteAsync(p1.Id);

        var revoked = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.EventType == "continent_first_revoked" && e.Points == -5000)
            .FirstOrDefaultAsync();
        Assert.IsNotNull(revoked, "continent_first_revoked event expected");
    }

    [TestMethod]
    public async Task DeleteAsync_ReassignsContinentPoints_WhenOtherPlacesInRegion()
    {
        // user1 has P1 in Brazil and P2 in Argentina; deleting P1 → continent points move to P2
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça");
        var p2 = SeedPlace(f.Ctx, _user1Id, _argentinaId, "BuenosAires");
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");
        SeedPointEvent(f.Ctx, _user1Id, "continent_first", 5000, p1.Id, "Continent");

        await f.Sut.DeleteAsync(p1.Id);

        var reassigned = await f.Ctx.Set<PointEvent>()
            .Where(e => e.UserId == _user1Id && e.EventType == "continent_first"
                     && e.Points == 5000 && e.ReferenceId == p2.Id && e.ReferenceType == "Continent")
            .FirstOrDefaultAsync();
        Assert.IsNotNull(reassigned, "continent_first re-awarded on surviving place expected");
    }

    // ─── DeleteAsync — PromptHomeCountry ─────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_NonHomePlace_ReturnsPromptFalse()
    {
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça", isHome: false);
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");

        var result = await f.Sut.DeleteAsync(p1.Id);

        Assert.IsFalse(result.PromptHomeCountry);
    }

    [TestMethod]
    public async Task DeleteAsync_HomePlace_NoOtherHome_ReturnsPromptTrue_WithCountryInfo()
    {
        await using var f = new Fixture();
        var p1 = SeedPlace(f.Ctx, _user1Id, _brazilId, "Itacuruça", isHome: true);
        SeedPointEvent(f.Ctx, _user1Id, "city_added", 50, p1.Id, "Place");

        var result = await f.Sut.DeleteAsync(p1.Id);

        Assert.IsTrue(result.PromptHomeCountry);
        Assert.AreEqual(_brazilId, result.CountryId);
        Assert.AreEqual("Brazil", result.CountryName);
    }
}
