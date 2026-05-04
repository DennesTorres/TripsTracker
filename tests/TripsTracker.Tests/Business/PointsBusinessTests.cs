using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PointsBusinessTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;
        private readonly Mock<IUserContext> _userContextMock = new();

        public PointsBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PointsBusiness(Ctx, _userContextMock.Object);
        }

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            var ddl = new[]
            {
                @"CREATE TABLE Users (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL DEFAULT '',
                    DisplayName TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    TotalPoints INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE TABLE PointEvents (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    EventType TEXT NOT NULL DEFAULT '',
                    Points INTEGER NOT NULL DEFAULT 0,
                    ReferenceId INTEGER,
                    ReferenceType TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01'
                )",
                "CREATE INDEX IX_PointEvents_UserId ON PointEvents (UserId)",
                "CREATE INDEX IX_PointEvents_EventType ON PointEvents (EventType)",
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
    public async Task AwardAsync_CreatesPointEvent()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().Add(new User { Id = 1, Email = "u@x.com", CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).AwardAsync(1, "city_added", 50, 99, "Place");

        var evt = await f.Ctx.Set<PointEvent>().SingleAsync();
        Assert.AreEqual(1, evt.UserId);
        Assert.AreEqual("city_added", evt.EventType);
        Assert.AreEqual(50, evt.Points);
        Assert.AreEqual(99, evt.ReferenceId);
        Assert.AreEqual("Place", evt.ReferenceType);
    }

    [TestMethod]
    public async Task AwardAsync_UpdatesCachedTotalPoints()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().Add(new User { Id = 1, Email = "u@x.com", CreatedAt = DateTime.UtcNow, TotalPoints = 100 });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).AwardAsync(1, "city_added", 50);

        var user = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == 1);
        Assert.AreEqual(150, user.TotalPoints);
    }

    [TestMethod]
    public async Task AwardAsync_AccumulatesMultipleAwards()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().Add(new User { Id = 1, Email = "u@x.com", CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        var biz = f.ForUser(1);
        await biz.AwardAsync(1, "city_added", 50);
        await biz.AwardAsync(1, "country_first", 500);
        await biz.AwardAsync(1, "continent_first", 5000);

        var user = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == 1);
        Assert.AreEqual(5550, user.TotalPoints);
        Assert.AreEqual(3, await f.Ctx.Set<PointEvent>().CountAsync());
    }

    [TestMethod]
    public async Task GetSummaryAsync_ReturnsTotalAndRecentEvents()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().Add(new User { Id = 1, Email = "u@x.com", CreatedAt = DateTime.UtcNow, TotalPoints = 550 });
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = 1, EventType = "city_added", Points = 50, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PointEvent { UserId = 1, EventType = "country_first", Points = 500, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        );
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(1).GetSummaryAsync();

        Assert.AreEqual(550, summary.TotalPoints);
        Assert.AreEqual(2, summary.RecentEvents.Count);
        // Recent events ordered descending by CreatedAt: country_first first
        Assert.AreEqual("country_first", summary.RecentEvents[0].EventType);
    }

    [TestMethod]
    public async Task GetSummaryAsync_OnlyReturnsCurrentUserEvents()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().AddRange(
            new User { Id = 1, Email = "u1@x.com", CreatedAt = DateTime.UtcNow, TotalPoints = 50 },
            new User { Id = 2, Email = "u2@x.com", CreatedAt = DateTime.UtcNow, TotalPoints = 500 }
        );
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = 1, EventType = "city_added", Points = 50, CreatedAt = DateTime.UtcNow },
            new PointEvent { UserId = 2, EventType = "country_first", Points = 500, CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(1).GetSummaryAsync();

        Assert.AreEqual(50, summary.TotalPoints);
        Assert.AreEqual(1, summary.RecentEvents.Count);
        Assert.AreEqual("city_added", summary.RecentEvents[0].EventType);
    }

    // ─── GetLeaderboardAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetLeaderboardAsync_RanksUsersByTotalPointsDescending()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().AddRange(
            new User { Id = 1, Email = "a@x.com", DisplayName = "Alice", CreatedAt = DateTime.UtcNow, TotalPoints = 100 },
            new User { Id = 2, Email = "b@x.com", DisplayName = "Bob", CreatedAt = DateTime.UtcNow, TotalPoints = 500 },
            new User { Id = 3, Email = "c@x.com", DisplayName = "Carol", CreatedAt = DateTime.UtcNow, TotalPoints = 250 }
        );
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(1).GetLeaderboardAsync();

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(1, result[0].Rank);
        Assert.AreEqual("Bob", result[0].DisplayName);
        Assert.AreEqual(500, result[0].TotalPoints);
        Assert.AreEqual(2, result[1].Rank);
        Assert.AreEqual("Carol", result[1].DisplayName);
        Assert.AreEqual(3, result[2].Rank);
        Assert.AreEqual("Alice", result[2].DisplayName);
    }

    [TestMethod]
    public async Task GetLeaderboardAsync_ExcludesUsersWithZeroPoints()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().AddRange(
            new User { Id = 1, Email = "a@x.com", DisplayName = "Alice", CreatedAt = DateTime.UtcNow, TotalPoints = 100 },
            new User { Id = 2, Email = "b@x.com", DisplayName = "NoPoints", CreatedAt = DateTime.UtcNow, TotalPoints = 0 }
        );
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(1).GetLeaderboardAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alice", result[0].DisplayName);
    }

    [TestMethod]
    public async Task GetLeaderboardAsync_UsesEmailWhenDisplayNameIsNull()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().Add(
            new User { Id = 1, Email = "a@x.com", DisplayName = null, CreatedAt = DateTime.UtcNow, TotalPoints = 50 }
        );
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(1).GetLeaderboardAsync();

        Assert.AreEqual("a@x.com", result[0].DisplayName);
    }

    [TestMethod]
    public async Task GetLeaderboardAsync_RespectsLimit()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().AddRange(Enumerable.Range(1, 5).Select(i =>
            new User { Id = i, Email = $"u{i}@x.com", CreatedAt = DateTime.UtcNow, TotalPoints = i * 10 }
        ));
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(1).GetLeaderboardAsync(limit: 3);

        Assert.AreEqual(3, result.Count);
    }
}
