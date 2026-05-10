using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PointsBusinessTests
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
            "Database=TripsTracker_Test_Points");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly TransactionScope _scope;
        private readonly Mock<IUserContext> _userContextMock = new();

        public PointsBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PointsBusiness(Ctx, _userContextMock.Object);
        }

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection(); // keep single connection enlisted — prevents DTC escalation
        }

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose(); // no Complete() → automatic rollback
        }
    }

    private static User MakeUser(string email, int totalPoints = 0, string? displayName = null) => new()
    {
        Email = email, CreatedAt = DateTime.UtcNow, TotalPoints = totalPoints, DisplayName = displayName,
    };

    #endregion

    [TestMethod]
    public async Task AwardAsync_CreatesPointEvent()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com");
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).AwardAsync(user.Id, "city_added", 50, 99, "Place");

        var evt = await f.Ctx.Set<PointEvent>().SingleAsync();
        Assert.AreEqual(user.Id, evt.UserId);
        Assert.AreEqual("city_added", evt.EventType);
        Assert.AreEqual(50, evt.Points);
        Assert.AreEqual(99, evt.ReferenceId);
        Assert.AreEqual("Place", evt.ReferenceType);
    }

    [TestMethod]
    public async Task AwardAsync_UpdatesCachedTotalPoints()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 100);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).AwardAsync(user.Id, "city_added", 50);

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(150, updated.TotalPoints);
    }

    [TestMethod]
    public async Task AwardAsync_AccumulatesMultipleAwards()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com");
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();

        var biz = f.ForUser(user.Id);
        await biz.AwardAsync(user.Id, "city_added", 50);
        await biz.AwardAsync(user.Id, "country_first", 500);
        await biz.AwardAsync(user.Id, "continent_first", 5000);

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(5550, updated.TotalPoints);
        Assert.AreEqual(3, await f.Ctx.Set<PointEvent>().CountAsync());
    }

    [TestMethod]
    public async Task GetSummaryAsync_ReturnsTotalAndRecentEvents()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 550);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PointEvent { UserId = user.Id, EventType = "country_first", Points = 500, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        );
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(user.Id).GetSummaryAsync();

        Assert.AreEqual(550, summary.TotalPoints);
        Assert.AreEqual(2, summary.RecentEvents.Count);
        Assert.AreEqual("country_first", summary.RecentEvents[0].EventType);
    }

    [TestMethod]
    public async Task GetSummaryAsync_OnlyReturnsCurrentUserEvents()
    {
        await using var f = new Fixture();
        var u1 = MakeUser("u1@x.com", totalPoints: 50);
        var u2 = MakeUser("u2@x.com", totalPoints: 500);
        f.Ctx.Set<User>().AddRange(u1, u2);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = u1.Id, EventType = "city_added", Points = 50, CreatedAt = DateTime.UtcNow },
            new PointEvent { UserId = u2.Id, EventType = "country_first", Points = 500, CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(u1.Id).GetSummaryAsync();

        Assert.AreEqual(50, summary.TotalPoints);
        Assert.AreEqual(1, summary.RecentEvents.Count);
        Assert.AreEqual("city_added", summary.RecentEvents[0].EventType);
    }

    // ─── GetLeaderboardAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetLeaderboardAsync_RanksUsersByTotalPointsDescending()
    {
        await using var f = new Fixture();
        var alice = MakeUser("a@x.com", totalPoints: 100, displayName: "Alice");
        var bob = MakeUser("b@x.com", totalPoints: 500, displayName: "Bob");
        var carol = MakeUser("c@x.com", totalPoints: 250, displayName: "Carol");
        f.Ctx.Set<User>().AddRange(alice, bob, carol);
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(alice.Id).GetLeaderboardAsync();

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
        var alice = MakeUser("a@x.com", totalPoints: 100, displayName: "Alice");
        var noPoints = MakeUser("b@x.com", totalPoints: 0, displayName: "NoPoints");
        f.Ctx.Set<User>().AddRange(alice, noPoints);
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(alice.Id).GetLeaderboardAsync();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Alice", result[0].DisplayName);
    }

    [TestMethod]
    public async Task GetLeaderboardAsync_UsesEmailWhenDisplayNameIsNull()
    {
        await using var f = new Fixture();
        var user = MakeUser("a@x.com", totalPoints: 50);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();

        var result = await f.ForUser(user.Id).GetLeaderboardAsync();

        Assert.AreEqual("a@x.com", result[0].DisplayName);
    }

    [TestMethod]
    public async Task GetLeaderboardAsync_RespectsLimit()
    {
        await using var f = new Fixture();
        f.Ctx.Set<User>().AddRange(Enumerable.Range(1, 5).Select(i =>
            MakeUser($"u{i}@x.com", totalPoints: i * 10)
        ));
        await f.Ctx.SaveChangesAsync();
        var anyUser = await f.Ctx.Set<User>().FirstAsync();

        var result = await f.ForUser(anyUser.Id).GetLeaderboardAsync(limit: 3);

        Assert.AreEqual(3, result.Count);
    }

    // ─── GetRecentAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetRecentAsync_ExcludesOriginalEvents_WhenRevoked()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com");
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        var original = new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, ReferenceId = 1, ReferenceType = "Place", CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
        f.Ctx.Set<PointEvent>().Add(original);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent { UserId = user.Id, EventType = "city_added_revoked", Points = -50, ReferenceId = 1, ReferenceType = "Place", OriginalEventId = original.Id, CreatedAt = DateTime.UtcNow.AddMinutes(-1) });
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(user.Id).GetSummaryAsync();

        Assert.AreEqual(0, summary.RecentEvents.Count, "Original events that have been revoked should not appear in recent events");
    }

    [TestMethod]
    public async Task GetRecentAsync_ExcludesRevokedEvents()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com");
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new PointEvent { UserId = user.Id, EventType = "city_added_revoked", Points = -50, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PointEvent { UserId = user.Id, EventType = "country_first", Points = 500, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        );
        await f.Ctx.SaveChangesAsync();

        var summary = await f.ForUser(user.Id).GetSummaryAsync();

        Assert.AreEqual(2, summary.RecentEvents.Count, "Revoked events should be excluded from recent events");
        Assert.IsFalse(summary.RecentEvents.Any(e => e.EventType.EndsWith("_revoked")),
            "No revoked events should appear in recent events");
    }

    // ─── RevokeAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task RevokeAsync_SetsOriginalEventId()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 50);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        var original = new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, ReferenceId = 5, ReferenceType = "Place", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        f.Ctx.Set<PointEvent>().Add(original);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).RevokeAsync(user.Id, "city_", 5, "Place");

        var events = await f.Ctx.Set<PointEvent>().OrderBy(e => e.Id).ToListAsync();
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(original.Id, events[1].OriginalEventId, "Revocation must link back to the original event via OriginalEventId");
    }

    [TestMethod]
    public async Task ReassignAsync_RevokesOldAndAwardsWithNewReference()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 500);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        var original = new PointEvent { UserId = user.Id, EventType = "country_first", Points = 500, ReferenceId = 10, ReferenceType = "Country", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        f.Ctx.Set<PointEvent>().Add(original);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).ReassignAsync(user.Id, "country_", 10, "Country", 20, "Country");

        var events = await f.Ctx.Set<PointEvent>().OrderBy(e => e.Id).ToListAsync();
        Assert.AreEqual(3, events.Count);
        Assert.AreEqual("country_first_revoked", events[1].EventType);
        Assert.AreEqual(original.Id, events[1].OriginalEventId);
        Assert.AreEqual("country_first", events[2].EventType);
        Assert.AreEqual(500, events[2].Points);
        Assert.AreEqual(20, events[2].ReferenceId);
        Assert.AreEqual("Country", events[2].ReferenceType);
    }

    [TestMethod]
    public async Task ReassignAsync_IsNoOp_WhenNoMatchingEvent()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 50);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, ReferenceId = 5, ReferenceType = "Place", CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).ReassignAsync(user.Id, "country_", 99, "Country", 20, "Country");

        Assert.AreEqual(1, await f.Ctx.Set<PointEvent>().CountAsync(), "No changes when no matching event");
    }

    [TestMethod]
    public async Task ReassignAsync_PreservesTotalPoints_NetZero()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 500);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent { UserId = user.Id, EventType = "country_first", Points = 500, ReferenceId = 10, ReferenceType = "Country", CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).ReassignAsync(user.Id, "country_", 10, "Country", 20, "Country");

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(500, updated.TotalPoints, "Reassignment is net-zero — total points unchanged");
    }

    [TestMethod]
    public async Task RevokeAsync_InsertsNegativeEvent()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 50);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent
        {
            UserId = user.Id, EventType = "city_added", Points = 50,
            ReferenceId = 5, ReferenceType = "Place", CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).RevokeAsync(user.Id, "city_", 5, "Place");

        var events = await f.Ctx.Set<PointEvent>().OrderBy(e => e.Id).ToListAsync();
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("city_added_revoked", events[1].EventType);
        Assert.AreEqual(-50, events[1].Points);
    }

    [TestMethod]
    public async Task RevokeAsync_DeductsFromTotalPoints()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 2550);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().AddRange(
            new PointEvent { UserId = user.Id, EventType = "city_added", Points = 50, ReferenceId = 5, ReferenceType = "Place", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PointEvent { UserId = user.Id, EventType = "country_first", Points = 500, ReferenceId = 1, ReferenceType = "Country", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new PointEvent { UserId = user.Id, EventType = "continent_first", Points = 2000, ReferenceId = null, ReferenceType = "Americas", CreatedAt = DateTime.UtcNow }
        );
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).RevokeAsync(user.Id, "country_", 1, "Country");

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(2050, updated.TotalPoints);
    }

    [TestMethod]
    public async Task RevokeAsync_IsNoOp_WhenNoMatchingEvent()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 50);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent
        {
            UserId = user.Id, EventType = "city_added", Points = 50, ReferenceId = 5, ReferenceType = "Place", CreatedAt = DateTime.UtcNow
        });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).RevokeAsync(user.Id, "city_", 99, "Place");

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(50, updated.TotalPoints);
        Assert.AreEqual(1, await f.Ctx.Set<PointEvent>().CountAsync());
    }

    [TestMethod]
    public async Task RevokeAsync_MatchesNullReferenceId()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@x.com", totalPoints: 5000);
        f.Ctx.Set<User>().Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PointEvent>().Add(new PointEvent
        {
            UserId = user.Id, EventType = "continent_first", Points = 5000,
            ReferenceId = null, ReferenceType = "Americas", CreatedAt = DateTime.UtcNow
        });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(user.Id).RevokeAsync(user.Id, "continent_", null, "Americas");

        var updated = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.AreEqual(0, updated.TotalPoints);
    }
}
