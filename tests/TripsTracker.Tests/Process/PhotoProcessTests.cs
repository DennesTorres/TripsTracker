using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Integration;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

file sealed class PhotoTestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public PhotoTestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
[DoNotParallelize]
public class PhotoProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static BlobServiceClient _blobClient = null!;
    private static int _placeId;

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
            "Database=TripsTracker_Test_PhotoProcess");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        _blobClient = new BlobServiceClient("UseDevelopmentStorage=true");

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        await ctx.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT Users ON;
            INSERT INTO Users (Id, Email, CreatedAt, IsDiscoverable, StorageUsedBytes)
            VALUES (1, 'user1@test.com', GETUTCDATE(), 0, 0);
            SET IDENTITY_INSERT Users OFF;");

        var brazil = new Country { IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "\U0001F1E7\U0001F1F7", Name = "Brazil", Region = "Americas" };
        ctx.Set<Country>().Add(brazil);
        await ctx.SaveChangesAsync();

        var place = new Place
        {
            Lon = -46.63, Lat = -23.55, CountryId = brazil.Id,
            City = "São Paulo", StateAbbr = "SP", StateName = "São Paulo",
            IsHome = false, UserId = 1
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

        public PhotoProcess Build(int userId = 1)
        {
            var userCtx = new PhotoTestUserContext(userId);
            var photos = new PlacePhotoBusiness(Ctx, userCtx, new UserBusiness(Ctx));
            var blobs = new BlobStorageService(_blobClient);
            return new PhotoProcess(photos, blobs, userCtx);
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
    public async Task UploadAsync_CreatesPhotoRecord_WithCorrectBlobName()
    {
        await using var f = new Fixture();
        var sut = f.Build();

        var result = await sut.UploadAsync(_placeId, MakeStream(), "image/jpeg", "photo.jpg", 100, null);

        var entity = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstAsync(p => p.Id == result.Id);
        f.TrackBlob(entity.BlobName);

        var parts = entity.BlobName.Split('/');
        Assert.AreEqual(3, parts.Length, "BlobName must have 3 path segments");
        Assert.AreEqual("1", parts[0], "First segment must be userId");
        Assert.AreEqual(_placeId.ToString(), parts[1], "Second segment must be placeId");
        Assert.IsTrue(parts[2].EndsWith(".jpg"), "File name must preserve .jpg extension");
        Assert.IsTrue(Guid.TryParse(Path.GetFileNameWithoutExtension(parts[2]), out _), "File name stem must be a GUID");
    }

    [TestMethod]
    public async Task UploadAsync_UploadsBlobToStorage()
    {
        await using var f = new Fixture();
        var sut = f.Build();

        var result = await sut.UploadAsync(_placeId, MakeStream(), "image/jpeg", "photo.jpg", 100, null);

        var entity = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstAsync(p => p.Id == result.Id);
        f.TrackBlob(entity.BlobName);

        var container = _blobClient.GetBlobContainerClient("place-photos");
        var exists = await container.GetBlobClient(entity.BlobName).ExistsAsync();
        Assert.IsTrue(exists.Value, "Blob must exist in storage after upload.");
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
