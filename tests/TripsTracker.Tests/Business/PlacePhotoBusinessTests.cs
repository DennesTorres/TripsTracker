using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PlacePhotoBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;

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
            "Database=TripsTracker_Test_Photos");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public Mock<IUserBusiness> UserBizMock { get; } = new Mock<IUserBusiness>();
        private readonly Mock<IUserContext> _userContextMock = new Mock<IUserContext>();
        private IDbContextTransaction? _transaction;

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
        }

        public PlacePhotoBusiness ForUser(int userId, long storageUsedBytes = 0)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            UserBizMock.Setup(u => u.GetStorageUsedAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storageUsedBytes);
            UserBizMock.Setup(u => u.AddStorageUsedAsync(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return new PlacePhotoBusiness(Ctx, _userContextMock.Object, UserBizMock.Object);
        }

        public async Task BeginTransactionAsync()
            => _transaction = await Ctx.Database.BeginTransactionAsync();

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
            }
            await Ctx.DisposeAsync();
        }
    }

    #endregion

    [TestMethod]
    public async Task CreateAsync_SetsSortOrder_Incrementally()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = 1, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        var biz = f.ForUser(1);
        var p1 = await biz.CreateAsync(place.Id, "blob1", "a.jpg", "image/jpeg", 100, null);
        var p2 = await biz.CreateAsync(place.Id, "blob2", "b.jpg", "image/jpeg", 200, null);

        Assert.AreEqual(1, p1.SortOrder);
        Assert.AreEqual(2, p2.SortOrder);
    }

    [TestMethod]
    public async Task GetByPlaceAsync_ReturnsPhotos_FromAllUsers()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = 1, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlacePhoto>().AddRange(
            new PlacePhoto { PlaceId = place.Id, UserId = 1, BlobName = "u1.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow },
            new PlacePhoto { PlaceId = place.Id, UserId = 2, BlobName = "u2.jpg", ContentType = "image/jpeg", SortOrder = 2, UploadedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(1).GetByPlaceAsync(place.Id);

        Assert.AreEqual(2, photos.Count, "Photos from all users should be returned");
    }

    [TestMethod]
    public async Task GetByPlaceAsync_IncludesAverageRating()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = 1, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();
        var photo = new PlacePhoto { PlaceId = place.Id, UserId = 1, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PhotoRating>().AddRange(
            new PhotoRating { UserId = 2, PhotoId = photo.Id, Rating = 4, CreatedAt = DateTime.UtcNow },
            new PhotoRating { UserId = 3, PhotoId = photo.Id, Rating = 2, CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(1).GetByPlaceAsync(place.Id);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual(3.0, photos[0].AverageRating, 0.001);
        Assert.AreEqual(2, photos[0].RatingCount);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenPhotoOwnedByOtherUser()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(photo.Id);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_AndRemovesPhoto()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 1, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(photo.Id);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task RateAsync_CreatesRating_WhenNoneExists()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).RateAsync(photo.Id, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == 1 && r.PhotoId == photo.Id);
        Assert.AreEqual(5, rating.Rating);
    }

    [TestMethod]
    public async Task RateAsync_UpdatesRating_WhenAlreadyRated()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PhotoRating>().Add(new PhotoRating { UserId = 1, PhotoId = photo.Id, Rating = 3, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).RateAsync(photo.Id, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == 1 && r.PhotoId == photo.Id);
        Assert.AreEqual(5, rating.Rating);
        Assert.AreEqual(1, await f.Ctx.Set<PhotoRating>().CountAsync());
    }

    [TestMethod]
    public async Task GetBlobInfoAsync_ReturnsBlobInfo()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 1, BlobName = "abc123.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var info = await f.ForUser(1).GetBlobInfoAsync(photo.Id);

        Assert.IsNotNull(info);
        Assert.AreEqual("abc123.jpg", info.BlobName);
        Assert.AreEqual("image/jpeg", info.ContentType);
    }

    // ─── Quota enforcement ────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_ThrowsBusinessRuleException_WhenQuotaExceeded()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = 1, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        const long maxBytes = 100L * 1024 * 1024;
        var biz = f.ForUser(1, storageUsedBytes: maxBytes);

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => biz.CreateAsync(place.Id, "blob", "a.jpg", "image/jpeg", 1, null));
    }

    [TestMethod]
    public async Task CreateAsync_UpdatesStorageUsed_AfterSuccess()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = 1, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).CreateAsync(place.Id, "blob", "a.jpg", "image/jpeg", 5000, null);

        f.UserBizMock.Verify(
            u => u.AddStorageUsedAsync(1, 5000, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_DecreasesStorageUsed_WhenPhotoDeleted()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = 1, UserId = 1, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, SizeBytes = 5000, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).DeleteAsync(photo.Id);

        f.UserBizMock.Verify(
            u => u.AddStorageUsedAsync(1, -5000, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
