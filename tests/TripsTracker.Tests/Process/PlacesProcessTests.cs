using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
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

    // Real-DB fixture: wires actual PlaceBusiness + CountryBusiness so DeleteAsync tests
    // verify real orchestration outcomes instead of mock call expectations.
    private sealed class ProcessFixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public PlaceBusiness PlaceBiz { get; }
        public CountryBusiness CountryBiz { get; }
        public PlacesProcess Process { get; }
        private readonly List<int> _trackedCountryIds = [];

        public ProcessFixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
            var userCtx = new FakeUserContext(UserId);
            PlaceBiz = new PlaceBusiness(Ctx, userCtx);
            CountryBiz = new CountryBusiness(Ctx, userCtx);
            Process = new PlacesProcess(PlaceBiz, CountryBiz, null!); // geocoding not used in DeleteAsync
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

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    // Helpers for Moq-based orchestration tests (AddAsync only — these test call sequencing,
    // not database state, so mocking is appropriate here).
    private static CountryDto Brazil() => new CountryDto
    {
        Id = 1, IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "🇧🇷",
        Name = "Brazil", Region = "Americas"
    };

    private static PlaceDto AnyPlace() => new(
        Id: 1, Lon: -43.9, Lat: -22.9, CountryId: 1, CountryName: "Brazil",
        CountryFlag: "🇧🇷", City: "Itacuruça", StateAbbr: "RJ",
        StateName: "Rio de Janeiro", IsHome: false);

    #endregion

    // ─── UpdateAsync — orchestration (Moq) ────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_WhenIsHome_CallsSetHomeAsync()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        var existing = new PlaceDto(1, 0, 0, 42, "France", "🇫🇷", "Paris", null, null, false);
        var updated = new PlaceDto(1, 0, 0, 42, "France", "🇫🇷", "Paris", null, null, true);

        places.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        places.Setup(p => p.UpdateAsync(1, It.IsAny<UpdatePlaceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(updated);
        countries.Setup(c => c.SetHomeAsync(42, true, It.IsAny<CancellationToken>())).ReturnsAsync((CountryDto?)null);

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.UpdateAsync(1, new UpdatePlaceDto(true));

        countries.Verify(c => c.SetHomeAsync(42, true, It.IsAny<CancellationToken>()), Times.Once,
            "UpdateAsync must sync UserCountry.IsHome when IsHome is true");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenNotIsHome_DoesNotCallSetHomeAsync()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        var existing = new PlaceDto(1, 0, 0, 42, "France", "🇫🇷", "Paris", null, null, true);
        var updated = new PlaceDto(1, 0, 0, 42, "France", "🇫🇷", "Paris", null, null, false);

        places.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        places.Setup(p => p.UpdateAsync(1, It.IsAny<UpdatePlaceDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(updated);

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.UpdateAsync(1, new UpdatePlaceDto(false));

        countries.Verify(c => c.SetHomeAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never,
            "SetHomeAsync must not be called when IsHome is false");
    }

    [TestMethod]
    public async Task UpdateAsync_WhenPlaceNotFound_ReturnsNull()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        places.Setup(p => p.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((PlaceDto?)null);

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        var result = await sut.UpdateAsync(999, new UpdatePlaceDto(false));

        Assert.IsNull(result);
        countries.Verify(c => c.SetHomeAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // ─── AddAsync — validation (R13 #1) ───────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_CityNameEqualsCountryName_ThrowsBusinessRuleException()
    {
        var countries = new Mock<ICountryBusiness>();
        var places = new Mock<IPlaceBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("MT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CountryDto { Id = 10, IsoNumeric = 470, IsoAlpha2 = "MT", Flag = "🇲🇹", Name = "Malta", Region = "Europe" });

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        var ex = await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => sut.AddAsync(new AddPlaceDto("Malta", "MT")));

        Assert.AreEqual("CITY_IS_COUNTRY", ex.ErrorCode);
    }

    // ─── AddAsync — GEOCODING_IS_INTERNAL (Moq) ──────────────────────────────

    [TestMethod]
    public async Task AddAsync_WhenGlobalPlaceNotFound_CallsGeocoding()
    {
        // GEOCODING_IS_INTERNAL: geocode only when city is not in global Places
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.FindGlobalAsync("Itacuruça", Brazil().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePlaceDto?)null);
        geocoding.Setup(g => g.GeocodeAsync("Itacuruça", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        geocoding.Verify(
            g => g.GeocodeAsync("Itacuruça", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "GeocodeAsync must be called when city is not in global Places");
    }

    [TestMethod]
    public async Task AddAsync_WhenGlobalPlaceExists_SkipsGeocoding()
    {
        // GEOCODING_IS_INTERNAL: skip geocoding when city already has a global Place
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        var existingDto = new CreatePlaceDto(-22.93, -43.90, Brazil().Id, "Itacuruça", "RJ", "Rio de Janeiro");

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.FindGlobalAsync("Itacuruça", Brazil().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDto);
        places.Setup(p => p.CreateAsync(existingDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        geocoding.Verify(
            g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "GeocodeAsync must NOT be called when city already exists in global Places");
    }

    [TestMethod]
    public async Task AddAsync_StoresCityNameFromGeocodingResult()
    {
        // The city name stored must come from the geocoding result (Photon canonical name),
        // not directly from the DTO — only when geocoding is performed.
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.FindGlobalAsync("Itacuruça", Brazil().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePlaceDto?)null);
        geocoding.Setup(g => g.GeocodeAsync("Itacuruça", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        places.Verify(
            p => p.CreateAsync(
                It.Is<CreatePlaceDto>(c => c.City == "Itacuruça"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "City name must come from geocoding result");
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

    // ─── AddAsync — IsHome=true path (Moq) ────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_WhenIsHomeTrue_ClearsHomesAndMarksAsHome()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.FindGlobalAsync("São Paulo", Brazil().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreatePlaceDto?)null);
        geocoding.Setup(g => g.GeocodeAsync("São Paulo", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-23.55, -46.63, "São Paulo", "SP", "São Paulo", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());
        // SetHomeAsync calls GetByIdAsync to retrieve countryId for SyncHomeFlagAsync
        places.Setup(p => p.GetByIdAsync(AnyPlace().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());
        // AddAsync calls SetVisitedAsync before SetHomeAsync to create UserCountry row
        countries.Setup(c => c.SetVisitedAsync(It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryDto?)null);

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("São Paulo", "BR", IsHome: true));

        places.Verify(p => p.ClearAllHomePlacesAsync(It.IsAny<CancellationToken>()), Times.Once,
            "AddAsync with IsHome=true must clear all existing home places first");
        places.Verify(p => p.MarkAsHomeAsync(AnyPlace().Id, It.IsAny<CancellationToken>()), Times.Once,
            "AddAsync with IsHome=true must mark the new place as home");
    }
}
