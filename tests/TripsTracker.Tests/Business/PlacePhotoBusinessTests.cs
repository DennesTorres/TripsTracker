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
    private static int _countryId;
    private static int _placeId;
    private static int _user1Id;
    private static int _user2Id;
    private static int _user3Id;

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

        // Seed FK parents shared across all tests in this class
        var country = new Country { IsoNumeric = 9002, IsoAlpha2 = "ZY", Flag = "🏳", Name = "TestCountry", Region = "Test" };
        ctx.Countries.Add(country);
        var u1 = new User { Email = "seed1@photos.test", CreatedAt = DateTime.UtcNow };
        var u2 = new User { Email = "seed2@photos.test", CreatedAt = DateTime.UtcNow };
        var u3 = new User { Email = "seed3@photos.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync();

        var place = new Place { City = "SeedCity", CountryId = country.Id, UserId = u1.Id, Lon = 0, Lat = 0 };
        ctx.Places.Add(place);
        await ctx.SaveChangesAsync();

        _countryId = country.Id;
        _placeId = place.Id;
        _user1Id = u1.Id;
        _user2Id = u2.Id;
        _user3Id = u3.Id;
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
        var place = new Place { City = "A", CountryId = _countryId, UserId = _user1Id, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        var biz = f.ForUser(_user1Id);
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
        var place = new Place { City = "A", CountryId = _countryId, UserId = _user1Id, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlacePhoto>().AddRange(
            new PlacePhoto { PlaceId = place.Id, UserId = _user1Id, BlobName = "u1.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow },
            new PlacePhoto { PlaceId = place.Id, UserId = _user2Id, BlobName = "u2.jpg", ContentType = "image/jpeg", SortOrder = 2, UploadedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(_user1Id).GetByPlaceAsync(place.Id);

        Assert.AreEqual(2, photos.Count, "Photos from all users should be returned");
    }

    [TestMethod]
    public async Task GetByPlaceAsync_IncludesAverageRating()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = _countryId, UserId = _user1Id, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();
        var photo = new PlacePhoto { PlaceId = place.Id, UserId = _user1Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PhotoRating>().AddRange(
            new PhotoRating { UserId = _user2Id, PhotoId = photo.Id, Rating = 4, CreatedAt = DateTime.UtcNow },
            new PhotoRating { UserId = _user3Id, PhotoId = photo.Id, Rating = 2, CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(_user1Id).GetByPlaceAsync(place.Id);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual(3.0, photos[0].AverageRating, 0.001);
        Assert.AreEqual(2, photos[0].RatingCount);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenPhotoOwnedByOtherUser()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user2Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(_user1Id).DeleteAsync(photo.Id);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_AndRemovesPhoto()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user1Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(_user1Id).DeleteAsync(photo.Id);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task RateAsync_CreatesRating_WhenNoneExists()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user2Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).RateAsync(photo.Id, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == _user1Id && r.PhotoId == photo.Id);
        Assert.AreEqual(5, rating.Rating);
    }

    [TestMethod]
    public async Task RateAsync_UpdatesRating_WhenAlreadyRated()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user2Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PhotoRating>().Add(new PhotoRating { UserId = _user1Id, PhotoId = photo.Id, Rating = 3, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).RateAsync(photo.Id, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == _user1Id && r.PhotoId == photo.Id);
        Assert.AreEqual(5, rating.Rating);
        Assert.AreEqual(1, await f.Ctx.Set<PhotoRating>().CountAsync());
    }

    [TestMethod]
    public async Task GetBlobInfoAsync_ReturnsBlobInfo()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user1Id, BlobName = "abc123.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        var info = await f.ForUser(_user1Id).GetBlobInfoAsync(photo.Id);

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
        var place = new Place { City = "A", CountryId = _countryId, UserId = _user1Id, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        const long maxBytes = 100L * 1024 * 1024;
        var biz = f.ForUser(_user1Id, storageUsedBytes: maxBytes);

        await Assert.ThrowsExactlyAsync<BusinessRuleException>(
            () => biz.CreateAsync(place.Id, "blob", "a.jpg", "image/jpeg", 1, null));
    }

    [TestMethod]
    public async Task CreateAsync_UpdatesStorageUsed_AfterSuccess()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var place = new Place { City = "A", CountryId = _countryId, UserId = _user1Id, Lon = 0, Lat = 0 };
        f.Ctx.Set<Place>().Add(place);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).CreateAsync(place.Id, "blob", "a.jpg", "image/jpeg", 5000, null);

        f.UserBizMock.Verify(
            u => u.AddStorageUsedAsync(_user1Id, 5000, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_DecreasesStorageUsed_WhenPhotoDeleted()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var photo = new PlacePhoto { PlaceId = _placeId, UserId = _user1Id, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, SizeBytes = 5000, UploadedAt = DateTime.UtcNow };
        f.Ctx.Set<PlacePhoto>().Add(photo);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).DeleteAsync(photo.Id);

        f.UserBizMock.Verify(
            u => u.AddStorageUsedAsync(_user1Id, -5000, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
