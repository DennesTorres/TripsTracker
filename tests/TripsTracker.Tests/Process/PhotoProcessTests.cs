using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Integration;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
[DoNotParallelize]
public class PhotoProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static BlobServiceClient _blobClient = null!;
    private static int _placeId;
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
            "Database=TripsTracker_Test_PhotoProcess7");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        _blobClient = new BlobServiceClient("UseDevelopmentStorage=true");

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var user = new User { Email = "photo7@test.com", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        _userId = user.Id;

        var brazil = new Country { IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "\U0001F1E7\U0001F1F7", Name = "Brazil", Region = "Americas" };
        ctx.Set<Country>().Add(brazil);
        await ctx.SaveChangesAsync();

        var place = new Place
        {
            Lon = -46.63, Lat = -23.55, CountryId = brazil.Id,
            City = "São Paulo", StateAbbr = "SP", StateName = "São Paulo",
            IsHome = false, UserId = _userId
        };
        ctx.Set<Place>().Add(place);
        await ctx.SaveChangesAsync();
        _placeId = place.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TransactionScope _scope;
        public TripsTrackerDbContext Ctx { get; }
        private readonly List<string> _blobsToClean = [];

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public PhotoProcess Build()
        {
            var userCtx = new TestUserContext(_userId);
            var photos = new PlacePhotoBusiness(Ctx, userCtx, new UserBusiness(Ctx));
            var blobs = new BlobStorageService(_blobClient);
            var points = new PointsBusiness(Ctx, userCtx);
            return new PhotoProcess(photos, blobs, points, userCtx);
        }

        public void TrackBlob(string blobName) => _blobsToClean.Add(blobName);

        public async ValueTask DisposeAsync()
        {
            var container = _blobClient.GetBlobContainerClient("place-photos");
            foreach (var name in _blobsToClean)
                await container.GetBlobClient(name).DeleteIfExistsAsync();

            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    private static Stream MakeStream(string content = "fake-image-bytes") =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

    #endregion

    // ─── UploadAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UploadAsync_CreatesPhotoRecord_AndAwards5Points()
    {
        await using var f = new Fixture();
        var sut = f.Build();

        var result = await sut.UploadAsync(_placeId, MakeStream(), "image/jpeg", "photo.jpg", 100, null);

        var entity = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstAsync(p => p.Id == result.Id);
        f.TrackBlob(entity.BlobName);

        // PointEvent created with correct fields
        var evt = await f.Ctx.Set<PointEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.ReferenceId == result.Id && e.ReferenceType == "Photo");
        Assert.IsNotNull(evt, "A PointEvent must be created for the photo upload.");
        Assert.AreEqual("photo_uploaded", evt.EventType);
        Assert.AreEqual(5, evt.Points);
        Assert.AreEqual(_userId, evt.UserId);

        // User.TotalPoints incremented
        var user = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _userId);
        Assert.AreEqual(5, user.TotalPoints);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_RemovesPhotoAndBlob()
    {
        await using var f = new Fixture();
        var sut = f.Build();

        var uploaded = await sut.UploadAsync(_placeId, MakeStream(), "image/jpeg", "photo.jpg", 100, null);
        var entity = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstAsync(p => p.Id == uploaded.Id);
        f.TrackBlob(entity.BlobName); // safety cleanup if delete fails mid-way

        var deleted = await sut.DeleteAsync(uploaded.Id);

        Assert.IsTrue(deleted, "DeleteAsync must return true for an existing photo.");

        var container = _blobClient.GetBlobContainerClient("place-photos");
        var blobExists = await container.GetBlobClient(entity.BlobName).ExistsAsync();
        Assert.IsFalse(blobExists.Value, "Blob must be removed from storage after delete.");

        var dbRecord = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == uploaded.Id);
        Assert.IsNull(dbRecord, "PlacePhoto DB record must be removed after delete.");
    }
}
