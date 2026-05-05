using Moq;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
public class PlacesProcessTests
{
    private static CountryDto Brazil() => new(
        Id: 1, IsoNumeric: 76, IsoAlpha2: "BR", Flag: "🇧🇷",
        Name: "Brazil", Region: "Americas", IsHome: false, IsVisited: false, ShowStateBorders: false);

    private static PlaceDto AnyPlace() => new(
        Id: 1, Lon: -43.9, Lat: -22.9, CountryId: 1, CountryName: "Brazil",
        CountryFlag: "🇧🇷", City: "Itacuruça", StateAbbr: "RJ",
        StateName: "Rio de Janeiro", IsHome: false);

    private static (PlacesProcess Sut, Mock<IPointsBusiness> Points) BuildSut(
        Mock<IPlaceBusiness> places,
        Mock<ICountryBusiness> countries,
        Mock<IGeocodingBusiness> geocoding)
    {
        var points = new Mock<IPointsBusiness>();
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(1);
        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object, points.Object, userContext.Object);
        return (sut, points);
    }

    /// Sets up mocks for a standard add-place call and controls cascade conditions.
    private static (Mock<IPlaceBusiness> Places, Mock<ICountryBusiness> Countries, Mock<IGeocodingBusiness> Geocoding)
        StandardMocks(
            bool hasPlaceInCountry = true,
            bool hasPlaceInRegion = true,
            bool anyGloballyInCountry = true,
            bool anyGloballyInRegion = true)
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        geocoding.Setup(g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResult(-22.93, -43.90, "Itacuruça", "RJ", "Rio de Janeiro", "BR"));
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());
        places.Setup(p => p.HasAnyInCountryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPlaceInCountry);
        places.Setup(p => p.HasAnyForCurrentUserInRegionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPlaceInRegion);
        places.Setup(p => p.HasAnyGloballyInCountryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(anyGloballyInCountry);
        places.Setup(p => p.HasAnyGloballyInRegionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(anyGloballyInRegion);

        return (places, countries, geocoding);
    }

    // ─── AddAsync — basic ────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_AlwaysCallsGeocoding()
    {
        // Geocoding must always be called — there is no coordinate bypass path.
        var (places, countries, geocoding) = StandardMocks();
        var (sut, _) = BuildSut(places, countries, geocoding);

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
        var (places, countries, geocoding) = StandardMocks();
        var (sut, _) = BuildSut(places, countries, geocoding);

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
        var (places, countries, geocoding) = StandardMocks();
        var (sut, _) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("São Paulo", "BR"));

        geocoding.Verify(
            g => g.GeocodeAsync("São Paulo", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── AddAsync — cascade scoring ──────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_AwardsOnlyCityPoints_WhenNotFirstInCountryOrRegion()
    {
        // Not first in country, not first in region → only city_added (50 pts)
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: true, hasPlaceInRegion: true);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_added", 50, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, It.Is<string>(e => e.StartsWith("country")), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        points.Verify(p => p.AwardAsync(1, It.Is<string>(e => e.StartsWith("continent")), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task AddAsync_AwardsCountryFirst_WhenFirstInCountryPersonally()
    {
        // First in country for this user, but other users already have places there → personal 500
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: false, hasPlaceInRegion: true,
            anyGloballyInCountry: true);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_added", 50, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "country_first", 500, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_AwardsCountryPioneer_WhenFirstInCountryGlobally()
    {
        // No user has ever visited this country → pioneer, 2000 + city_pioneer 200
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: false, hasPlaceInRegion: true,
            anyGloballyInCountry: false);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_pioneer", 200, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "country_pioneer", 2000, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "city_added", It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        points.Verify(p => p.AwardAsync(1, "country_first", It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task AddAsync_AwardsContinentFirst_WhenFirstInRegionPersonally()
    {
        // First in this continent for this user, but others already have places there → personal 5000
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: true, hasPlaceInRegion: false,
            anyGloballyInRegion: true);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_added", 50, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "continent_first", 5000, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_AwardsContinentPioneer_WhenFirstInRegionGlobally()
    {
        // No user has ever visited this continent → pioneer, 20000 + city_pioneer 200
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: true, hasPlaceInRegion: false,
            anyGloballyInCountry: true, anyGloballyInRegion: false);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_pioneer", 200, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "continent_pioneer", 20000, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "city_added", It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task AddAsync_AwardsAllTiers_WhenFirstInCountryAndRegionGlobally()
    {
        // Global pioneer for both country AND region → city_pioneer + country_pioneer + continent_pioneer
        var (places, countries, geocoding) = StandardMocks(
            hasPlaceInCountry: false, hasPlaceInRegion: false,
            anyGloballyInCountry: false, anyGloballyInRegion: false);
        var (sut, points) = BuildSut(places, countries, geocoding);

        await sut.AddAsync(new AddPlaceDto("Itacuruça", "BR"));

        points.Verify(p => p.AwardAsync(1, "city_pioneer", 200, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "country_pioneer", 2000, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.AwardAsync(1, "continent_pioneer", 20000, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── DeleteAsync — cascade revocation ────────────────────────────────────

    private static (PlacesProcess Sut, Mock<IPointsBusiness> Points) BuildDeleteSut(
        bool hasRemainingInCountry, bool hasRemainingInRegion,
        PlaceDto? survivingInCountry = null, PlaceDto? survivingInRegion = null)
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();
        var points = new Mock<IPointsBusiness>();
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(1);

        places.Setup(p => p.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());
        places.Setup(p => p.DeleteAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        places.Setup(p => p.HasAnyInCountryAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRemainingInCountry);
        places.Setup(p => p.HasAnyForCurrentUserInRegionAsync("Americas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRemainingInRegion);
        places.Setup(p => p.HasHomeInCountryAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        places.Setup(p => p.GetFirstForCurrentUserInCountryAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(survivingInCountry);
        places.Setup(p => p.GetFirstForCurrentUserInRegionAsync("Americas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(survivingInRegion);

        countries.Setup(c => c.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        countries.Setup(c => c.SetVisitedAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CountryDto?)null);

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object, points.Object, userContext.Object);
        return (sut, points);
    }

    private static PlaceDto SurvivingPlace() => new(
        Id: 2, Lon: -46.6, Lat: -23.5, CountryId: 1, CountryName: "Brazil",
        CountryFlag: "🇧🇷", City: "São Paulo", StateAbbr: "SP",
        StateName: "São Paulo", IsHome: false);

    [TestMethod]
    public async Task DeleteAsync_RevokesPlacePoints_Always()
    {
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: true, hasRemainingInRegion: true);

        await sut.DeleteAsync(1);

        points.Verify(p => p.RevokeAsync(1, "city_", 1, "Place", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_RevokesCountryPoints_WhenLastPlaceInCountry()
    {
        // Country tier is stored as (place.Id, "Country") — revocation uses place.Id
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: false, hasRemainingInRegion: true);

        await sut.DeleteAsync(1);

        points.Verify(p => p.RevokeAsync(1, "country_", AnyPlace().Id, "Country", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_ReassignsCountryPoints_WhenOtherPlacesInCountry()
    {
        var surviving = SurvivingPlace();
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: true, hasRemainingInRegion: true, survivingInCountry: surviving);

        await sut.DeleteAsync(1);

        points.Verify(p => p.ReassignAsync(1, "country_", AnyPlace().Id, "Country", surviving.Id, "Country", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.RevokeAsync(1, "country_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_RevokesRegionPoints_WhenLastPlaceInRegion()
    {
        // Continent tier is stored as (place.Id, "Continent") — revocation uses place.Id
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: false, hasRemainingInRegion: false);

        await sut.DeleteAsync(1);

        points.Verify(p => p.RevokeAsync(1, "continent_", AnyPlace().Id, "Continent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_ReassignsContinentPoints_WhenOtherPlacesInRegion()
    {
        var surviving = SurvivingPlace();
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: false, hasRemainingInRegion: true, survivingInRegion: surviving);

        await sut.DeleteAsync(1);

        points.Verify(p => p.ReassignAsync(1, "continent_", AnyPlace().Id, "Continent", surviving.Id, "Continent", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.RevokeAsync(1, "continent_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── DeleteAsync — edge cases (R5 #5 scenarios) ──────────────────────────

    [TestMethod]
    public async Task DeleteAsync_RevokesAllTiers_WhenOnlyPlaceInCountryAndRegion()
    {
        // Last place in country AND region → both country and continent revoked (not reassigned)
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: false, hasRemainingInRegion: false);

        await sut.DeleteAsync(1);

        points.Verify(p => p.RevokeAsync(1, "city_", AnyPlace().Id, "Place", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.RevokeAsync(1, "country_", AnyPlace().Id, "Country", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.RevokeAsync(1, "continent_", AnyPlace().Id, "Continent", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.ReassignAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_DoesNotReassignCountry_WhenNoSurvivingPlaceFound()
    {
        // hasRemainingInCountry=true but GetFirstForCurrentUser returns null (race condition guard)
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: true, hasRemainingInRegion: true, survivingInCountry: null);

        await sut.DeleteAsync(1);

        points.Verify(p => p.ReassignAsync(1, "country_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_DoesNotReassignContinent_WhenNoSurvivingPlaceFound()
    {
        // hasRemainingInRegion=true but GetFirstForCurrentUser returns null (race condition guard)
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: false, hasRemainingInRegion: true, survivingInRegion: null);

        await sut.DeleteAsync(1);

        points.Verify(p => p.ReassignAsync(1, "continent_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_ReassignsBothCountryAndContinent_WhenBothHaveRemainingPlaces()
    {
        // Places remain in both country and region → both tiers reassigned, none revoked
        var surviving = SurvivingPlace();
        var (sut, points) = BuildDeleteSut(hasRemainingInCountry: true, hasRemainingInRegion: true,
            survivingInCountry: surviving, survivingInRegion: surviving);

        await sut.DeleteAsync(1);

        points.Verify(p => p.ReassignAsync(1, "country_", AnyPlace().Id, "Country", surviving.Id, "Country", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.ReassignAsync(1, "continent_", AnyPlace().Id, "Continent", surviving.Id, "Continent", It.IsAny<CancellationToken>()), Times.Once);
        points.Verify(p => p.RevokeAsync(1, "country_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        points.Verify(p => p.RevokeAsync(1, "continent_", It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
