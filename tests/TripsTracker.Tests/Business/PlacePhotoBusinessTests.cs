using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PlacePhotoBusinessTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;
        private readonly Mock<IUserContext> _userContextMock = new();

        public PlacePhotoBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PlacePhotoBusiness(Ctx, _userContextMock.Object);
        }

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            var ddl = new[]
            {
                @"CREATE TABLE Places (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Lon REAL NOT NULL DEFAULT 0, Lat REAL NOT NULL DEFAULT 0,
                    CountryId INTEGER NOT NULL DEFAULT 0,
                    City TEXT NOT NULL DEFAULT '', StateAbbr TEXT, StateName TEXT,
                    IsHome INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE TABLE PlacePhotos (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    PlaceId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    BlobName TEXT NOT NULL DEFAULT '',
                    OriginalFileName TEXT,
                    ContentType TEXT NOT NULL DEFAULT '',
                    SizeBytes INTEGER NOT NULL DEFAULT 0,
                    Caption TEXT,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    UploadedAt TEXT NOT NULL DEFAULT '0001-01-01'
                )",
                "CREATE INDEX IX_PlacePhotos_PlaceId ON PlacePhotos (PlaceId)",
                "CREATE INDEX IX_PlacePhotos_UserId ON PlacePhotos (UserId)",
                @"CREATE TABLE PhotoRatings (
                    UserId INTEGER NOT NULL,
                    PhotoId INTEGER NOT NULL,
                    Rating INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    PRIMARY KEY (UserId, PhotoId)
                )",
            };

            foreach (var sql in ddl)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
                .UseSqlite(_conn)
                .Options;
            Ctx = new TripsTrackerDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }

    #endregion

    [TestMethod]
    public async Task CreateAsync_SetsSortOrder_Incrementally()
    {
        await using var f = new Fixture();
        f.Ctx.Set<Place>().Add(new Place { Id = 1, City = "A", CountryId = 1, Lon = 0, Lat = 0 });
        await f.Ctx.SaveChangesAsync();

        var biz = f.ForUser(1);
        var p1 = await biz.CreateAsync(1, "blob1", "a.jpg", "image/jpeg", 100, null);
        var p2 = await biz.CreateAsync(1, "blob2", "b.jpg", "image/jpeg", 200, null);

        Assert.AreEqual(1, p1.SortOrder);
        Assert.AreEqual(2, p2.SortOrder);
    }

    [TestMethod]
    public async Task GetByPlaceAsync_FiltersToCurrentUser()
    {
        await using var f = new Fixture();
        f.Ctx.Set<Place>().Add(new Place { Id = 1, City = "A", CountryId = 1, Lon = 0, Lat = 0 });
        f.Ctx.Set<PlacePhoto>().AddRange(
            new PlacePhoto { Id = 1, PlaceId = 1, UserId = 1, BlobName = "u1.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow },
            new PlacePhoto { Id = 2, PlaceId = 1, UserId = 2, BlobName = "u2.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(1).GetByPlaceAsync(1);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual(1, photos[0].UserId);
    }

    [TestMethod]
    public async Task GetByPlaceAsync_IncludesAverageRating()
    {
        await using var f = new Fixture();
        f.Ctx.Set<Place>().Add(new Place { Id = 1, City = "A", CountryId = 1, Lon = 0, Lat = 0 });
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 1, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        f.Ctx.Set<PhotoRating>().AddRange(
            new PhotoRating { UserId = 2, PhotoId = 1, Rating = 4, CreatedAt = DateTime.UtcNow },
            new PhotoRating { UserId = 3, PhotoId = 1, Rating = 2, CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var photos = await f.ForUser(1).GetByPlaceAsync(1);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual(3.0, photos[0].AverageRating, 0.001);
        Assert.AreEqual(2, photos[0].RatingCount);
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenPhotoOwnedByOtherUser()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(1);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_AndRemovesPhoto()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 1, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(1);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlacePhoto>().CountAsync());
    }

    [TestMethod]
    public async Task RateAsync_CreatesRating_WhenNoneExists()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).RateAsync(1, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == 1 && r.PhotoId == 1);
        Assert.AreEqual(5, rating.Rating);
    }

    [TestMethod]
    public async Task RateAsync_UpdatesRating_WhenAlreadyRated()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 2, BlobName = "x.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        f.Ctx.Set<PhotoRating>().Add(new PhotoRating { UserId = 1, PhotoId = 1, Rating = 3, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).RateAsync(1, 5);

        var rating = await f.Ctx.Set<PhotoRating>().FirstAsync(r => r.UserId == 1 && r.PhotoId == 1);
        Assert.AreEqual(5, rating.Rating);
        Assert.AreEqual(1, await f.Ctx.Set<PhotoRating>().CountAsync());
    }

    [TestMethod]
    public async Task GetBlobInfoAsync_ReturnsBlobInfo()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlacePhoto>().Add(new PlacePhoto { Id = 1, PlaceId = 1, UserId = 1, BlobName = "abc123.jpg", ContentType = "image/jpeg", SortOrder = 1, UploadedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        var info = await f.ForUser(1).GetBlobInfoAsync(1);

        Assert.IsNotNull(info);
        Assert.AreEqual("abc123.jpg", info.BlobName);
        Assert.AreEqual("image/jpeg", info.ContentType);
    }
}
