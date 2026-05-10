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
        Name: "Brazil", Region: "Americas", IsHome: false, IsVisited: false, ShowStateBorders: false);

    private static PlaceDto AnyPlace() => new(
        Id: 1, Lon: -43.9, Lat: -22.9, CountryId: 1, CountryName: "Brazil",
        CountryFlag: "🇧🇷", City: "Itacuruça", StateAbbr: "RJ",
        StateName: "Rio de Janeiro", IsHome: false);

    // ─── AddAsync ─────────────────────────────────────────────────────────────

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
