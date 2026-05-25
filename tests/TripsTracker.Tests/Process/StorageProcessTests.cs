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

file sealed class StorageTestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public StorageTestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
[DoNotParallelize]
public class StorageProcessTests
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
            "Database=TripsTracker_Test_StorageProcess");

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

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public StorageProcess Build(int userId = 1)
        {
            var userCtx = new StorageTestUserContext(userId);
            var photos = new PlacePhotoBusiness(Ctx, userCtx, new UserBusiness(Ctx));
            var blobs = new BlobStorageService(_blobClient);
            var storage = new StorageBusiness(Ctx);
            return new StorageProcess(photos, blobs, storage);
        }

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    // ─── RefreshAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RefreshAsync_ReconcilesSizes_UpdatesStorageTotal()
    {
        const int userId = 1;
        const long staleSize = 1L;
        var blobContent = System.Text.Encoding.UTF8.GetBytes("fake-image-bytes-for-storage-reconcile-test");
        var expectedSize = (long)blobContent.Length;

        // Upload a real blob to Azurite so GetUserBlobsAsync finds it
        var blobName = $"{userId}/{_placeId}/{Guid.NewGuid()}.jpg";
        var container = _blobClient.GetBlobContainerClient("place-photos");
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient(blobName).UploadAsync(new MemoryStream(blobContent), overwrite: true);

        try
        {
            await using var f = new Fixture();
            var sut = f.Build(userId);

            // Insert a PlacePhoto with a deliberately stale SizeBytes
            var photo = new PlacePhoto
            {
                PlaceId = _placeId, UserId = userId, BlobName = blobName,
                OriginalFileName = "test.jpg", ContentType = "image/jpeg",
                SizeBytes = staleSize, UploadedAt = DateTime.UtcNow
            };
            f.Ctx.Set<PlacePhoto>().Add(photo);
            await f.Ctx.SaveChangesAsync();

            // Act
            var result = await sut.RefreshAsync(userId);

            // Assert: total storage reflects actual blob size
            Assert.AreEqual(expectedSize, result.UsedBytes,
                "RefreshAsync must report actual blob size, not stale DB value.");

            // Assert: PlacePhoto.SizeBytes reconciled to actual blob size
            var updated = await f.Ctx.Set<PlacePhoto>().AsNoTracking().FirstAsync(p => p.Id == photo.Id);
            Assert.AreEqual(expectedSize, updated.SizeBytes,
                "PlacePhoto.SizeBytes must be updated to match actual blob size.");
        }
        finally
        {
            // Clean up blob regardless of test outcome
            await container.GetBlobClient(blobName).DeleteIfExistsAsync();
        }
    }
}
