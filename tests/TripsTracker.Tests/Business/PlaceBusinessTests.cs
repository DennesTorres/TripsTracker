using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
[DoNotParallelize] // Fixture uses hardcoded IsoNumeric=9801; parallel inserts would violate the unique index
public class PlaceBusinessTests
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

    // Use userId=1 (seeded user exists). Test country uses fake ISO codes outside real range.
    private const int UserId = 1;
    private const int OtherUserId = 2;

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public PlaceBusiness Biz { get; }
        private int? _countryId;

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
            Biz = new PlaceBusiness(Ctx, new FakeUserContext(UserId));
        }

        public async Task<Country> AddCountryAsync()
        {
            var country = new Country
            {
                IsoNumeric = 9801,
                IsoAlpha2 = "PB",
                Flag = "🏳",
                Name = "Test Country PB",
                Region = "Test",
            };
            Ctx.Set<Country>().Add(country);
            await Ctx.SaveChangesAsync();
            _countryId = country.Id;
            return country;
        }

        /// <summary>Creates a global Place + a UserPlaces link for the given user.</summary>
        public async Task<Place> AddPlaceAsync(int countryId, string city = "TestCity", bool isHome = false, int? userId = null)
        {
            var place = new Place
            {
                Lon = 0, Lat = 0,
                CountryId = countryId,
                City = city,
            };
            Ctx.Set<Place>().Add(place);
            await Ctx.SaveChangesAsync();

            var userPlace = new UserPlace
            {
                UserId = userId ?? UserId,
                PlaceId = place.Id,
                IsHome = isHome,
            };
            Ctx.Set<UserPlace>().Add(userPlace);
            await Ctx.SaveChangesAsync();

            return place;
        }

        public async ValueTask DisposeAsync()
        {
            if (_countryId.HasValue)
            {
                // UserPlaces cascade-deletes when Places are deleted
                await Ctx.Set<Place>()
                    .Where(p => p.CountryId == _countryId.Value)
                    .ExecuteDeleteAsync();
                await Ctx.Set<Country>()
                    .Where(c => c.Id == _countryId.Value)
                    .ExecuteDeleteAsync();
            }
            await Ctx.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    #endregion

    [TestMethod]
    public async Task GetAllAsync_ReturnsCurrentUserPlaces_NotOtherUsers()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var p1 = await f.AddPlaceAsync(country.Id, "City1");
        var p2 = await f.AddPlaceAsync(country.Id, "City2");
        // Other user links to a separate global place (different city to avoid dedup)
        var other = await f.AddPlaceAsync(country.Id, "OtherCity", userId: OtherUserId);

        var result = await f.Biz.GetAllAsync();

        Assert.IsTrue(result.Any(r => r.Id == p1.Id));
        Assert.IsTrue(result.Any(r => r.Id == p2.Id));
        Assert.IsFalse(result.Any(r => r.Id == other.Id));
    }

    [TestMethod]
    public async Task CreateAsync_ReturnsPlaceDto_WithCorrectValues()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();

        var dto = new CreatePlaceDto(10.5, 20.3, country.Id, "Paris", "IDF", "Île-de-France");
        var result = await f.Biz.CreateAsync(dto);

        Assert.IsNotNull(result);
        Assert.AreEqual("Paris", result.City);
        Assert.AreEqual(country.Id, result.CountryId);
        Assert.IsFalse(result.IsHome);
    }

    [TestMethod]
    public async Task CreateAsync_WhenGlobalPlaceExists_ReusesItAndCreatesUserLink()
    {
        // GEOCODING_IS_INTERNAL: second user links to the same global Place
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();

        var dto = new CreatePlaceDto(10.5, 20.3, country.Id, "SharedCity", null, null);
        var place1 = await f.Biz.CreateAsync(dto);

        // CreateAsync for a second user pointing at the same city should reuse the global Place
        var otherBiz = new PlaceBusiness(f.Ctx, new FakeUserContext(OtherUserId));
        var place2 = await otherBiz.CreateAsync(dto);

        Assert.AreEqual(place1.Id, place2.Id, "Both users must reference the same global Place row");
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsPlace_ForCurrentUser()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Berlin");

        var result = await f.Biz.GetByIdAsync(place.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual("Berlin", result.City);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsNull_ForOtherUsersPlace()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Tokyo", userId: OtherUserId);

        var result = await f.Biz.GetByIdAsync(place.Id);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetByIdAsync(int.MaxValue);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateAsync_UpdatesIsHome_CityIsImmutable()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "OriginalCity");

        var result = await f.Biz.UpdateAsync(place.Id, new UpdatePlaceDto(true));

        Assert.IsNotNull(result);
        Assert.AreEqual("OriginalCity", result.City, "PLACE_IMMUTABILITY: City must not be changed by UpdateAsync");
        Assert.IsTrue(result.IsHome);
    }

    [TestMethod]
    public async Task UpdateAsync_CityIsImmutable_NotChangedByUpdate()
    {
        // PLACE_IMMUTABILITY: City is set once by geocoding and cannot be changed via PUT
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "GeocodeCity");

        var result = await f.Biz.UpdateAsync(place.Id, new UpdatePlaceDto(false));

        Assert.IsNotNull(result);
        Assert.AreEqual("GeocodeCity", result.City, "PLACE_IMMUTABILITY: City must remain as set by geocoding");
        Assert.IsFalse(result.IsHome);
    }

    [TestMethod]
    public async Task UpdateAsync_SetIsHome_ClearsHomeOnOtherPlaces()
    {
        // HOME_EXCLUSIVITY: only one place per user may be IsHome at a time
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place1 = await f.AddPlaceAsync(country.Id, "HomeCity1", isHome: true);
        var place2 = await f.AddPlaceAsync(country.Id, "HomeCity2", isHome: false);

        var result = await f.Biz.UpdateAsync(place2.Id, new UpdatePlaceDto(true));

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsHome);

        var place1After = await f.Biz.GetByIdAsync(place1.Id);
        Assert.IsNotNull(place1After);
        Assert.IsFalse(place1After.IsHome, "HOME_EXCLUSIVITY: IsHome must be cleared on all other places when a new home is set");
    }

    [TestMethod]
    public async Task UpdateAsync_ReturnsNull_ForOtherUsersPlace()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Rome", userId: OtherUserId);

        var result = await f.Biz.UpdateAsync(place.Id, new UpdatePlaceDto(false));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_AndUnlinksPlace()
    {
        // DELETE_IS_UNLINK: removes UserPlaces row; global Place row remains
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Madrid");

        var deleted = await f.Biz.DeleteAsync(place.Id);
        var found = await f.Biz.GetByIdAsync(place.Id);

        Assert.IsTrue(deleted);
        Assert.IsNull(found, "After delete, place must not be visible to the user");

        // Global Place row must still exist (not deleted)
        var globalPlace = await f.Ctx.Set<Place>().FindAsync(place.Id);
        Assert.IsNotNull(globalPlace, "Global Place row must remain after user unlinks");
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.DeleteAsync(int.MaxValue);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasAnyInCountryAsync_ReturnsTrue_WhenPlaceExists()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        await f.AddPlaceAsync(country.Id);

        var result = await f.Biz.HasAnyInCountryAsync(country.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasAnyInCountryAsync_ReturnsFalse_WhenNoPlaces()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();

        var result = await f.Biz.HasAnyInCountryAsync(country.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasHomeInCountryAsync_ReturnsTrue_WhenHomeExists()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        await f.AddPlaceAsync(country.Id, isHome: true);

        var result = await f.Biz.HasHomeInCountryAsync(country.Id);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasHomeInCountryAsync_ReturnsFalse_WhenNoHomePlace()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        await f.AddPlaceAsync(country.Id, isHome: false);

        var result = await f.Biz.HasHomeInCountryAsync(country.Id);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ClearAllHomePlacesAsync_ClearsAllHomeFlags()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var p1 = await f.AddPlaceAsync(country.Id, "HomeCity1", isHome: true);
        var p2 = await f.AddPlaceAsync(country.Id, "HomeCity2", isHome: true);

        await f.Biz.ClearAllHomePlacesAsync();

        var p1After = await f.Biz.GetByIdAsync(p1.Id);
        var p2After = await f.Biz.GetByIdAsync(p2.Id);
        Assert.IsNotNull(p1After);
        Assert.IsNotNull(p2After);
        Assert.IsFalse(p1After.IsHome, "ClearAllHomePlacesAsync must clear IsHome on all places");
        Assert.IsFalse(p2After.IsHome, "ClearAllHomePlacesAsync must clear IsHome on all places");
    }

    [TestMethod]
    public async Task ClearAllHomePlacesAsync_WhenNoHomePlaces_IsNoOp()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        await f.AddPlaceAsync(country.Id, "NonHomeCity", isHome: false);

        await f.Biz.ClearAllHomePlacesAsync(); // must not throw
    }

    [TestMethod]
    public async Task MarkAsHomeAsync_SetsIsHome()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "City1", isHome: false);

        await f.Biz.MarkAsHomeAsync(place.Id);

        var result = await f.Biz.GetByIdAsync(place.Id);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsHome, "MarkAsHomeAsync must set IsHome=true on the target place");
    }

    [TestMethod]
    public async Task FindGlobalAsync_ReturnsExistingPlaceData()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        await f.AddPlaceAsync(country.Id, "GlobalCity");

        var result = await f.Biz.FindGlobalAsync("GlobalCity", country.Id);

        Assert.IsNotNull(result, "FindGlobalAsync must return data when a global Place exists");
        Assert.AreEqual("GlobalCity", result.City);
        Assert.AreEqual(country.Id, result.CountryId);
    }

    [TestMethod]
    public async Task FindGlobalAsync_ReturnsNull_WhenNotFound()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();

        var result = await f.Biz.FindGlobalAsync("NoSuchCity", country.Id);

        Assert.IsNull(result);
    }
}
