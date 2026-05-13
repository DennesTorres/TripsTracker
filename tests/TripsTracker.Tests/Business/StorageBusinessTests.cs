using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Integration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class StorageBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _countryId;
    private static int _userId;

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
            "Database=TripsTracker_Test_Storage");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var country = new Country { IsoNumeric = 9005, IsoAlpha2 = "ZV", Flag = "🏳", Name = "StorageTestCountry", Region = "Test" };
        ctx.Countries.Add(country);
        var user = new User { Email = "seed@storage.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        _countryId = country.Id;
        _userId = user.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public Mock<IBlobStorageService> BlobsMock { get; } = new();
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public StorageBusiness Build() => new(Ctx, BlobsMock.Object);

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    [TestMethod]
    public async Task GetUsageAsync_ReturnsStoredUsage_And10GbLimit()
    {
        await using var f = new Fixture();
        await f.Ctx.Users
            .Where(u => u.Id == _userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.StorageUsedBytes, 500_000L));

        var result = await f.Build().GetUsageAsync(_userId);

        Assert.AreEqual(500_000L, result.UsedBytes);
        Assert.AreEqual(10L * 1024 * 1024 * 1024, result.LimitBytes);
    }

    [TestMethod]
    public async Task RefreshAsync_UpdatesStorageUsedBytes_FromBlobSizes_AndSetsLastRefreshedAt()
    {
        await using var f = new Fixture();

        var place = new Place { City = "RefreshCity", CountryId = _countryId, UserId = _userId, Lon = 0, Lat = 0 };
        f.Ctx.Places.Add(place);
        await f.Ctx.SaveChangesAsync();

        var photo1 = new PlacePhoto { PlaceId = place.Id, UserId = _userId, BlobName = "1/p/a.jpg", ContentType = "image/jpeg", SortOrder = 1, SizeBytes = 100, UploadedAt = DateTime.UtcNow };
        var photo2 = new PlacePhoto { PlaceId = place.Id, UserId = _userId, BlobName = "1/p/b.jpg", ContentType = "image/jpeg", SortOrder = 2, SizeBytes = 200, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().AddRange(photo1, photo2);
        await f.Ctx.SaveChangesAsync();

        f.BlobsMock.Setup(b => b.GetUserBlobsAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BlobSizeInfo>
            {
                new("1/p/a.jpg", 150L),
                new("1/p/b.jpg", 250L),
            });

        var before = DateTime.UtcNow;
        var result = await f.Build().RefreshAsync(_userId);
        var after = DateTime.UtcNow;

        Assert.AreEqual(400L, result.UsedBytes, "Should sum actual blob sizes");
        Assert.AreEqual(10L * 1024 * 1024 * 1024, result.LimitBytes);
        Assert.IsNotNull(result.LastRefreshedAt, "LastRefreshedAt should be set");
        Assert.IsTrue(result.LastRefreshedAt >= before && result.LastRefreshedAt <= after);

        var user = await f.Ctx.Users.AsNoTracking().FirstAsync(u => u.Id == _userId);
        Assert.AreEqual(400L, user.StorageUsedBytes, "StorageUsedBytes should be persisted");
        Assert.IsNotNull(user.StorageLastRefreshedAt);
    }
}
