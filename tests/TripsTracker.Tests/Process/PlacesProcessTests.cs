using Moq;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
public class PlacesProcessTests
{
    private static CountryDto Brazil() => new(
        Id: 1, IsoNumeric: 76, IsoAlpha2: "BR", Flag: "🇧🇷",
        Name: "Brazil", Region: "Americas", IsHome: false, IsVisited: false);

    private static PlaceDto AnyPlace() => new(
        Id: 1, Lon: -43.9, Lat: -22.9, CountryId: 1, CountryName: "Brazil",
        CountryFlag: "🇧🇷", City: "Itacuruça", StateAbbr: "RJ",
        StateName: "Rio de Janeiro", IsHome: false);

    // ─── AddAsync ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddAsync_WithPreResolvedCoordinates_SkipsGeocoding()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        var dto = new AddPlaceDto(
            CityName: "Itacuruça",
            CountryIsoAlpha2: "BR",
            IsHome: false,
            Lat: -22.9278,
            Lon: -43.8994,
            StateAbbr: "RJ",
            StateName: "Rio de Janeiro");

        await sut.AddAsync(dto);

        geocoding.Verify(
            g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "GeocodeAsync must not be called when coordinates are pre-resolved from Photon");
    }

    [TestMethod]
    public async Task AddAsync_WithPreResolvedCoordinates_StoresCityNameAsProvided()
    {
        var places = new Mock<IPlaceBusiness>();
        var countries = new Mock<ICountryBusiness>();
        var geocoding = new Mock<IGeocodingBusiness>();

        countries.Setup(c => c.GetByIsoAlpha2Async("BR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Brazil());
        places.Setup(p => p.CreateAsync(It.IsAny<CreatePlaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnyPlace());

        var sut = new PlacesProcess(places.Object, countries.Object, geocoding.Object);

        var dto = new AddPlaceDto(
            CityName: "Itacuruça",
            CountryIsoAlpha2: "BR",
            IsHome: false,
            Lat: -22.9278,
            Lon: -43.8994,
            StateAbbr: "RJ",
            StateName: "Rio de Janeiro");

        await sut.AddAsync(dto);

        places.Verify(
            p => p.CreateAsync(
                It.Is<CreatePlaceDto>(c => c.City == "Itacuruça"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "City name 'Itacuruça' (Photon spelling) must be stored as-is, not re-geocoded via Nominatim");
    }

    [TestMethod]
    public async Task AddAsync_WithoutCoordinates_CallsGeocoding()
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

        // No pre-resolved coordinates — should geocode via Nominatim
        var dto = new AddPlaceDto("São Paulo", "BR");

        await sut.AddAsync(dto);

        geocoding.Verify(
            g => g.GeocodeAsync("São Paulo", It.IsAny<CountryDto>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "GeocodeAsync must be called when no pre-resolved coordinates are provided");
    }
}
