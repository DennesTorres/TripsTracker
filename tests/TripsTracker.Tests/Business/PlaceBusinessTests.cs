using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
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

        public async Task<Place> AddPlaceAsync(int countryId, string city = "TestCity", bool isHome = false, int? userId = null)
        {
            var place = new Place
            {
                Lon = 0, Lat = 0,
                CountryId = countryId,
                City = city,
                IsHome = isHome,
                UserId = userId ?? UserId,
            };
            Ctx.Set<Place>().Add(place);
            await Ctx.SaveChangesAsync();
            return place;
        }

        public async ValueTask DisposeAsync()
        {
            if (_countryId.HasValue)
            {
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

        var dto = new CreatePlaceDto(10.5, 20.3, country.Id, "Paris", "IDF", "Île-de-France", false);
        var result = await f.Biz.CreateAsync(dto);

        Assert.IsNotNull(result);
        Assert.AreEqual("Paris", result.City);
        Assert.AreEqual(country.Id, result.CountryId);
        Assert.IsFalse(result.IsHome);
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
    public async Task UpdateAsync_UpdatesCityAndIsHome()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "OldCity");

        var result = await f.Biz.UpdateAsync(place.Id, new UpdatePlaceDto("NewCity", true));

        Assert.IsNotNull(result);
        Assert.AreEqual("NewCity", result.City);
        Assert.IsTrue(result.IsHome);
    }

    [TestMethod]
    public async Task UpdateAsync_ReturnsNull_ForOtherUsersPlace()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Rome", userId: OtherUserId);

        var result = await f.Biz.UpdateAsync(place.Id, new UpdatePlaceDto("Rome2", false));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_AndRemovesPlace()
    {
        await using var f = new Fixture();
        var country = await f.AddCountryAsync();
        var place = await f.AddPlaceAsync(country.Id, "Madrid");

        var deleted = await f.Biz.DeleteAsync(place.Id);
        var found = await f.Biz.GetByIdAsync(place.Id);

        Assert.IsTrue(deleted);
        Assert.IsNull(found);
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
}
