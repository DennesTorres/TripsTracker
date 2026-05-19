using Moq;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
public class CountriesProcessTests
{
    private const string SampleGeoJson = """{"type":"FeatureCollection","features":[]}""";

    [TestMethod]
    public async Task GetBordersAsync_ReturnsGeoJson_WhenCountryHasIsoAlpha3()
    {
        var countries = new Mock<ICountryBusiness>();
        var geoBoundaries = new Mock<IGeoBoundariesService>();

        countries.Setup(c => c.GetIsoAlpha3Async(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("DEU");
        geoBoundaries.Setup(g => g.GetBordersAsync("DEU", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleGeoJson);

        var sut = new CountriesProcess(countries.Object, geoBoundaries.Object);

        var result = await sut.GetBordersAsync(1);

        Assert.AreEqual(SampleGeoJson, result);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenCountryHasNoIsoAlpha3()
    {
        var countries = new Mock<ICountryBusiness>();
        var geoBoundaries = new Mock<IGeoBoundariesService>();

        countries.Setup(c => c.GetIsoAlpha3Async(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = new CountriesProcess(countries.Object, geoBoundaries.Object);

        var result = await sut.GetBordersAsync(1);

        Assert.IsNull(result);
        geoBoundaries.Verify(g => g.GetBordersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetBordersAsync_ReturnsNull_WhenGeoBoundariesReturnsNull()
    {
        var countries = new Mock<ICountryBusiness>();
        var geoBoundaries = new Mock<IGeoBoundariesService>();

        countries.Setup(c => c.GetIsoAlpha3Async(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ZZZ");
        geoBoundaries.Setup(g => g.GetBordersAsync("ZZZ", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = new CountriesProcess(countries.Object, geoBoundaries.Object);

        var result = await sut.GetBordersAsync(99);

        Assert.IsNull(result);
    }
}
