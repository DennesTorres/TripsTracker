using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PlaceCommentBusinessTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;
        private readonly Mock<IUserContext> _userContextMock = new();

        public PlaceCommentBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PlaceCommentBusiness(Ctx, _userContextMock.Object);
        }

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            var ddl = new[]
            {
                @"CREATE TABLE Users (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL, DisplayName TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    StorageUsedBytes INTEGER NOT NULL DEFAULT 0,
                    IsDiscoverable INTEGER NOT NULL DEFAULT 0,
                    TotalPoints INTEGER NOT NULL DEFAULT 0
                )",
                "CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)",
                @"CREATE TABLE Places (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Lon REAL NOT NULL DEFAULT 0, Lat REAL NOT NULL DEFAULT 0,
                    CountryId INTEGER NOT NULL DEFAULT 0,
                    City TEXT NOT NULL DEFAULT '', StateAbbr TEXT, StateName TEXT,
                    IsHome INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE TABLE PlaceComments (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    PlaceId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Text TEXT NOT NULL DEFAULT '',
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    UpdatedAt TEXT
                )",
                "CREATE INDEX IX_PlaceComments_PlaceId ON PlaceComments (PlaceId)",
                "CREATE INDEX IX_PlaceComments_UserId ON PlaceComments (UserId)",
                @"CREATE TABLE CommentRatings (
                    UserId INTEGER NOT NULL,
                    CommentId INTEGER NOT NULL,
                    IsUpvote INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    PRIMARY KEY (UserId, CommentId)
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

    private static User MakeUser(int id, string email, string? displayName = null) => new()
    {
        Id = id, Email = email, DisplayName = displayName, CreatedAt = DateTime.UtcNow,
    };

    private static PlaceComment MakeComment(int id, int placeId, int userId, string text) => new()
    {
        Id = id, PlaceId = placeId, UserId = userId, Text = text, CreatedAt = DateTime.UtcNow,
    };

    #endregion

    // ─── GetByPlaceAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetByPlaceAsync_ReturnsComments_FromAllUsers()
    {
        await using var f = new Fixture();
        f.Ctx.Users.AddRange(
            MakeUser(1, "a@test.com", "Alice"),
            MakeUser(2, "b@test.com", "Bob")
        );
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(1, 1, 1, "Alice's comment"),
            MakeComment(2, 1, 2, "Bob's comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(1).GetByPlaceAsync(1);

        Assert.AreEqual(2, comments.Count, "Comments from all users should be returned");
    }

    [TestMethod]
    public async Task GetByPlaceAsync_OnlyReturnsSamePlaceComments()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(MakeUser(1, "a@test.com"));
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(1, 1, 1, "Place 1 comment"),
            MakeComment(2, 2, 1, "Place 2 comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(1).GetByPlaceAsync(1);

        Assert.AreEqual(1, comments.Count);
        Assert.AreEqual(1, comments[0].PlaceId);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenCommentOwnedByOtherUser()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlaceComment>().Add(MakeComment(1, 1, 2, "Other user's comment"));
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(1);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_WhenCommentIsOwn()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlaceComment>().Add(MakeComment(1, 1, 1, "My comment"));
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(1);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    // ─── VoteAsync ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task VoteAsync_CreatesUpvote_WhenNoneExists()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlaceComment>().Add(MakeComment(1, 1, 2, "A comment"));
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).VoteAsync(1, isUpvote: true);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == 1 && r.CommentId == 1);
        Assert.IsTrue(vote.IsUpvote);
    }

    [TestMethod]
    public async Task VoteAsync_UpdatesVote_WhenAlreadyVoted()
    {
        await using var f = new Fixture();
        f.Ctx.Set<PlaceComment>().Add(MakeComment(1, 1, 2, "A comment"));
        f.Ctx.Set<CommentRating>().Add(new CommentRating { UserId = 1, CommentId = 1, IsUpvote = true, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).VoteAsync(1, isUpvote: false);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == 1 && r.CommentId == 1);
        Assert.IsFalse(vote.IsUpvote);
        Assert.AreEqual(1, await f.Ctx.Set<CommentRating>().CountAsync());
    }
}
