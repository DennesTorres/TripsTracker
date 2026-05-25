using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Exceptions;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
[DoNotParallelize] // Real-DB tests with TransactionScope deadlock under method-level parallel execution
public class PlacesProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var dbOpts = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(dbOpts.ConnectionString)
            .Options;
    }

    private const int UserId = 1;

    // Real-DB fixture: wires actual PlaceBusiness + CountryBusiness so all tests
    // verify real orchestration outcomes against the actual schema.
    private sealed class ProcessFixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public PlaceBusiness PlaceBiz { get; }
        public CountryBusiness CountryBiz { get; }
        public PlacesProcess Process { get; }
        private readonly List<int> _trackedCountryIds = [];

        public ProcessFixture(IGeocodingBusiness? geocoding = null)
        {
            Ctx = new TripsTrackerDbContext(_options);
            var userCtx = new FakeUserContext(UserId);
            PlaceBiz = new PlaceBusiness(Ctx, userCtx);
            CountryBiz = new CountryBusiness(Ctx, userCtx);
            Process = new PlacesProcess(PlaceBiz, CountryBiz, geocoding!);
        }

        public async Task<Country> AddCountryAsync(string isoAlpha2)
        {
            var country = new Country
            {
                IsoNumeric = (int)isoAlpha2[0] * 100 + (int)isoAlpha2[1],
                IsoAlpha2 = isoAlpha2,
                Flag = "🏳",
                Name = $"Process Test {isoAlpha2}",
                Region = "Test",
            };
            Ctx.Set<Country>().Add(country);
            await Ctx.SaveChangesAsync();
            _trackedCountryIds.Add(country.Id);
            return country;
        }

        /// <summary>Creates a global Place + UserPlaces link for UserId.</summary>
        public async Task<Place> AddPlaceAsync(int countryId, bool isHome = false)
        {
            var place = new Place { Lon = 0, Lat = 0, CountryId = countryId, City = "TestCity" };
            Ctx.Set<Place>().Add(place);
            await Ctx.SaveChangesAsync();

            var userPlace = new UserPlace { UserId = UserId, PlaceId = place.Id, IsHome = isHome };
            Ctx.Set<UserPlace>().Add(userPlace);
            await Ctx.SaveChangesAsync();

            return place;
        }

        /// <summary>Creates a global Place without a UserPlaces link (not yet visited by any user).</summary>
        public async Task<Place> AddGlobalPlaceAsync(int countryId, string city)
        {
            var place = new Place { Lon = -1, Lat = -1, CountryId = countryId, City = city };
            Ctx.Set<Place>().Add(place);
            await Ctx.SaveChangesAsync();
            return place;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var cid in _trackedCountryIds)
            {
                // UserPlaces cascade-deletes when Places are deleted
                await Ctx.Set<Place>().Where(p => p.CountryId == cid).ExecuteDeleteAsync();
                await Ctx.Set<UserCountry>().Where(uc => uc.CountryId == cid).ExecuteDeleteAsync();
                await Ctx.Set<Country>().Where(c => c.Id == cid).ExecuteDeleteAsync();
            }
            await Ctx.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(int userId) : IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeGeocodingBusiness(GeocodingResult result) : IGeocodingBusiness
    {
        public bool WasCalled { get; private set; }

        public Task<GeocodingResult> GeocodeAsync(string cityName, CountryDto country, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<CitySuggestion>> SuggestCitiesAsync(string query, string countryCode = "", CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CitySuggestion>>(Array.Empty<CitySuggestion>());
    }

    #endregion

    // ─── UpdateAsync (real-DB) ────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_WhenIsHome_SetsCountryHome()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("R1");
        await f.CountryBiz.SetVisitedAsync(country.Id, true); // ensure UserCountry row exists
        var place = await f.AddPlaceAsync(country.Id, isHome: false);

        await f.Process.UpdateAsync(place.Id, new UpdatePlaceDto(true));

        var uc = await f.Ctx.Set<UserCountry>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == UserId && u.CountryId == country.Id);
        Assert.IsNotNull(uc);
        Assert.IsTrue(uc.IsHome, "UpdateAsync with IsHome=true must call SetHomeAsync on the country");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenNotIsHome_DoesNotSyncCountryHome()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("R2");
        await f.CountryBiz.SetVisitedAsync(country.Id, true); // create UserCountry row
        // Set IsHome=true directly — avoids clearing other countries via CountryBusiness.SetHomeAsync
        var ucRow = await f.Ctx.Set<UserCountry>()
            .FirstAsync(u => u.UserId == UserId && u.CountryId == country.Id);
        ucRow.IsHome = true;
        await f.Ctx.SaveChangesAsync();
        var place = await f.AddPlaceAsync(country.Id, isHome: false);

        await f.Process.UpdateAsync(place.Id, new UpdatePlaceDto(false));

        var ucAfter = await f.Ctx.Set<UserCountry>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == UserId && u.CountryId == country.Id);
        Assert.IsNotNull(ucAfter);
        Assert.IsTrue(ucAfter.IsHome,
            "UpdateAsync with IsHome=false must not call SetHomeAsync — country home flag must stay unchanged");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenPlaceNotFound_ReturnsNull()
    {
        await using var f = new ProcessFixture();

        var result = await f.Process.UpdateAsync(999999, new UpdatePlaceDto(false));

        Assert.IsNull(result);
    }

    // ─── DeleteAsync (real-DB) ─────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_NonHomePlace_ReturnsPromptFalse()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q1");
        var place = await f.AddPlaceAsync(country.Id, isHome: false);

        var result = await f.Process.DeleteAsync(place.Id);

        Assert.IsFalse(result.PromptHomeCountry);
        Assert.IsNull(result.CountryId);
        Assert.IsNull(result.CountryName);
    }

    [TestMethod]
    public async Task DeleteAsync_HomePlace_OtherHomeRemains_ReturnsPromptFalse()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q2");

        // Two separate global places, both linked as home for UserId
        var home1 = new Place { Lon = 0, Lat = 0, CountryId = country.Id, City = "HomeCity1" };
        var home2 = new Place { Lon = 0, Lat = 0, CountryId = country.Id, City = "HomeCity2" };
        f.Ctx.Set<Place>().AddRange(home1, home2);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<UserPlace>().AddRange(
            new UserPlace { UserId = UserId, PlaceId = home1.Id, IsHome = true },
            new UserPlace { UserId = UserId, PlaceId = home2.Id, IsHome = true });
        await f.Ctx.SaveChangesAsync();

        var result = await f.Process.DeleteAsync(home1.Id);

        Assert.IsFalse(result.PromptHomeCountry);
    }

    [TestMethod]
    public async Task DeleteAsync_HomePlace_NoOtherHome_ReturnsPromptTrue_WithCountryInfo()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q3");
        var home = await f.AddPlaceAsync(country.Id, isHome: true);

        var result = await f.Process.DeleteAsync(home.Id);

        Assert.IsTrue(result.PromptHomeCountry);
        Assert.AreEqual(country.Id, result.CountryId);
        Assert.AreEqual(country.Name, result.CountryName);
    }

    [TestMethod]
    public async Task DeleteAsync_LastPlaceInCountry_UnsetsVisited()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q4");
        await f.CountryBiz.SetVisitedAsync(country.Id, true);
        var place = await f.AddPlaceAsync(country.Id);

        await f.Process.DeleteAsync(place.Id);

        var uc = await f.Ctx.Set<UserCountry>()
            .FirstOrDefaultAsync(u => u.UserId == UserId && u.CountryId == country.Id);
        Assert.IsTrue(uc == null || !uc.IsVisited);
    }

    // ─── AddAsync — validation ────────────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_CityNameEqualsCountryName_ThrowsBusinessRuleException()
    {
        // Uses seeded Malta ("MT") — exception thrown before any DB write
        await using var f = new ProcessFixture();

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => f.Process.AddAsync(new AddPlaceDto("Malta", "MT")));

        Assert.AreEqual("CITY_IS_COUNTRY", ex.ErrorCode);
    }

    // ─── AddAsync — GEOCODING_IS_INTERNAL (real-DB + FakeGeocodingBusiness) ──

    [TestMethod]
    public async Task AddAsync_WhenGlobalPlaceNotFound_CallsGeocoding()
    {
        // GEOCODING_IS_INTERNAL: geocode only when city is not in global Places
        var fakeGeocoding = new FakeGeocodingBusiness(
            new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        await using var f = new ProcessFixture(fakeGeocoding);
        var country = await f.AddCountryAsync("S1");

        await f.Process.AddAsync(new AddPlaceDto("Itacuruça", country.IsoAlpha2));

        Assert.IsTrue(fakeGeocoding.WasCalled,
            "GeocodeAsync must be called when the city is not in global Places");
    }

    [TestMethod]
    public async Task AddAsync_WhenGlobalPlaceExists_SkipsGeocoding()
    {
        // GEOCODING_IS_INTERNAL: skip geocoding when city already has a global Place
        var fakeGeocoding = new FakeGeocodingBusiness(
            new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        await using var f = new ProcessFixture(fakeGeocoding);
        var country = await f.AddCountryAsync("S2");
        await f.AddGlobalPlaceAsync(country.Id, "Itacuruça"); // pre-exist in global Places

        await f.Process.AddAsync(new AddPlaceDto("Itacuruça", country.IsoAlpha2));

        Assert.IsFalse(fakeGeocoding.WasCalled,
            "GeocodeAsync must NOT be called when city already exists in global Places");
    }

    [TestMethod]
    public async Task AddAsync_StoresCityNameFromGeocodingResult()
    {
        // The city name stored must come from the geocoding result (Photon canonical name),
        // not directly from the raw DTO input — only when geocoding is performed.
        const string canonicalName = "Itacuruça";
        var fakeGeocoding = new FakeGeocodingBusiness(
            new GeocodingResult(-22.93, -43.90, canonicalName, "RJ", "Rio de Janeiro", "BR"));
        await using var f = new ProcessFixture(fakeGeocoding);
        var country = await f.AddCountryAsync("S3");

        await f.Process.AddAsync(new AddPlaceDto("itacuruca", country.IsoAlpha2)); // raw input differs

        var place = await f.Ctx.Set<Place>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.CountryId == country.Id);
        Assert.IsNotNull(place);
        Assert.AreEqual(canonicalName, place.City,
            "City name must come from geocoding result, not from the raw DTO input");
    }

    // ─── SetHomeAsync (real-DB) ────────────────────────────────────────────────

    [TestMethod]
    public async Task SetHomeAsync_SetsTargetPlaceAsHome()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q5");
        await f.CountryBiz.SetVisitedAsync(country.Id, true); // create UserCountry row
        var place = await f.AddPlaceAsync(country.Id, isHome: false);

        await f.Process.SetHomeAsync(place.Id);

        // IsHome now lives in UserPlaces, not Place
        var userPlace = await f.Ctx.Set<UserPlace>().AsNoTracking()
            .FirstAsync(up => up.UserId == UserId && up.PlaceId == place.Id);
        Assert.IsTrue(userPlace.IsHome, "SetHomeAsync must mark the target UserPlaces row as home");
    }

    [TestMethod]
    public async Task SetHomeAsync_ClearsExistingHomePlaces()
    {
        await using var f = new ProcessFixture();
        var country = await f.AddCountryAsync("Q6");
        await f.CountryBiz.SetVisitedAsync(country.Id, true); // create UserCountry row
        var existingHome = await f.AddPlaceAsync(country.Id, isHome: true);
        var newHome = await f.AddPlaceAsync(country.Id, isHome: false);

        await f.Process.SetHomeAsync(newHome.Id);

        // IsHome now lives in UserPlaces
        var oldHomeUserPlace = await f.Ctx.Set<UserPlace>().AsNoTracking()
            .FirstAsync(up => up.UserId == UserId && up.PlaceId == existingHome.Id);
        Assert.IsFalse(oldHomeUserPlace.IsHome, "SetHomeAsync must clear IsHome on all other UserPlaces rows");
    }

    // ─── AddAsync — IsHome=true path (real-DB) ────────────────────────────────

    [TestMethod]
    public async Task AddAsync_WhenIsHomeTrue_ClearsHomesAndMarksAsHome()
    {
        var fakeGeocoding = new FakeGeocodingBusiness(
            new GeocodingResult(-23.55, -46.63, "NewCity", null, null, "BR"));
        await using var f = new ProcessFixture(fakeGeocoding);
        var country = await f.AddCountryAsync("S4");
        await f.CountryBiz.SetVisitedAsync(country.Id, true); // UserCountry row must exist for SyncHomeFlagAsync
        var existingHome = await f.AddPlaceAsync(country.Id, isHome: true);

        await f.Process.AddAsync(new AddPlaceDto("NewCity", country.IsoAlpha2, IsHome: true));

        // Existing home place must be cleared
        var oldHomeUp = await f.Ctx.Set<UserPlace>().AsNoTracking()
            .FirstOrDefaultAsync(up => up.UserId == UserId && up.PlaceId == existingHome.Id);
        Assert.IsNotNull(oldHomeUp);
        Assert.IsFalse(oldHomeUp.IsHome,
            "AddAsync with IsHome=true must clear all existing home places (ClearAllHomePlacesAsync)");

        // Newly created place must be marked as home
        var newPlace = await f.Ctx.Set<Place>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.City == "NewCity" && p.CountryId == country.Id);
        Assert.IsNotNull(newPlace);
        var newHomeUp = await f.Ctx.Set<UserPlace>().AsNoTracking()
            .FirstOrDefaultAsync(up => up.UserId == UserId && up.PlaceId == newPlace.Id);
        Assert.IsNotNull(newHomeUp);
        Assert.IsTrue(newHomeUp.IsHome,
            "AddAsync with IsHome=true must mark the new place as home (MarkAsHomeAsync)");
    }
}
