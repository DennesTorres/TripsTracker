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

        public async Task<Place> AddPlaceAsync(int countryId, bool isHome = false)
        {
            var place = new Place { Lon = 0, Lat = 0, CountryId = countryId, City = "TestCity", IsHome = isHome, UserId = UserId };
            Ctx.Set<Place>().Add(place);
            await Ctx.SaveChangesAsync();
            return place;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var cid in _trackedCountryIds)
            {
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
        var home1 = await f.AddPlaceAsync(country.Id, isHome: true);
        await f.AddPlaceAsync(country.Id, isHome: true); // second home remains

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

    // ─── AddAsync — orchestration (Moq: verifies call sequencing, not DB state) ──

    [TestMethod]
    public async Task AddAsync_AlwaysCallsGeocoding()
    {
        // Geocoding must always be called — there is no coordinate bypass path.
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        geocoding.Setup(g => g.GeocodeAsync("Itacuruça", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        geocoding.Verify(
            g => g.GeocodeAsync("Itacuruça", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "GeocodeAsync must always be called — coordinates must be resolved server-side");
    }

    [TestMethod]
    public async Task AddAsync_StoresCityNameFromGeocodingResult()
    {
        // The city name stored must come from the geocoding result (Photon canonical name),
        // not directly from the DTO.
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
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

    [TestMethod]
    public async Task AddAsync_CallsGeocoding()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        geocoding.Setup(g => g.GeocodeAsync("São Paulo", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-23.55, -46.63, "São Paulo", "SP", "São Paulo", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        await sut.AddAsync(new AddPlaceDto("São Paulo", "BR"));

        geocoding.Verify(
            g => g.GeocodeAsync("São Paulo", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
