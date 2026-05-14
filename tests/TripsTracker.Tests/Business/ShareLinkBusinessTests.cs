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
public class ShareLinkBusinessTests
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
            "Database=TripsTracker_Test_ShareLinks");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
    }

    // No ClassCleanup: TransactionScope rollback leaves the DB empty after each test.
    // EnsureCreatedAsync in ClassInitialize is idempotent — the DB persists across runs.

    private sealed class Fixture : IAsyncDisposable
    {
        public ShareLinkBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            var userContext = new Mock<IUserContext>();
            userContext.Setup(u => u.UserId).Returns((int?)null);
            Biz = new ShareLinkBusiness(Ctx, userContext.Object);
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            _scope.Dispose(); // no Complete() → automatic rollback
        }
    }

    private static User MakeUser(string email, string? displayName = null, bool isDiscoverable = true) => new()
    {
        Email = email, DisplayName = displayName, CreatedAt = DateTime.UtcNow, IsDiscoverable = isDiscoverable,
    };

    private static ShareLink MakeLink(int userId, string token) => new()
    {
        UserId = userId, Token = token,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    #endregion

    // ─── CreateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_PersistsLink_AndReturnsDto()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();

        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(user.Id);
        var biz = new ShareLinkBusiness(f.Ctx, userContext.Object);

        var dto = await biz.CreateAsync(new TripsTracker.Domain.CreateShareLinkDto());

        Assert.IsNotNull(dto);
        Assert.IsFalse(string.IsNullOrEmpty(dto.Token), "Token must be generated");
        Assert.IsTrue(dto.IsActive);
        var inDb = await f.Ctx.ShareLinks.FindAsync(dto.Id);
        Assert.IsNotNull(inDb);
        Assert.AreEqual(dto.Token, inDb.Token);
    }

    // ─── GetUserLinksAsync ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetUserLinksAsync_ReturnsOnlyCurrentUserLinks_InDescendingOrder()
    {
        await using var f = new Fixture();
        var user1 = MakeUser("u1@test.com");
        var user2 = MakeUser("u2@test.com");
        f.Ctx.Users.AddRange(user1, user2);
        await f.Ctx.SaveChangesAsync();

        var older = MakeLink(user1.Id, "tok-older");
        older.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var newer = MakeLink(user1.Id, "tok-newer");
        newer.CreatedAt = DateTime.UtcNow;
        var other = MakeLink(user2.Id, "tok-other");
        f.Ctx.ShareLinks.AddRange(older, newer, other);
        await f.Ctx.SaveChangesAsync();

        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(user1.Id);
        var biz = new ShareLinkBusiness(f.Ctx, userContext.Object);

        var results = await biz.GetUserLinksAsync();

        Assert.HasCount(2, results);
        Assert.AreEqual("tok-newer", results[0].Token, "Newer link must come first");
        Assert.AreEqual("tok-older", results[1].Token);
    }

    // ─── DeactivateAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeactivateAsync_OwnLink_SetsInactiveAndReturnsTrue()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.Add(MakeLink(user.Id, "tok"));
        await f.Ctx.SaveChangesAsync();
        var linkId = (await f.Ctx.ShareLinks.FirstAsync(l => l.Token == "tok")).Id;

        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(user.Id);
        var biz = new ShareLinkBusiness(f.Ctx, userContext.Object);

        var result = await biz.DeactivateAsync(linkId);

        Assert.IsTrue(result);
        var inDb = await f.Ctx.ShareLinks.AsNoTracking().FirstAsync(l => l.Id == linkId);
        Assert.IsFalse(inDb.IsActive);
    }

    [TestMethod]
    public async Task DeactivateAsync_OtherUsersLink_ReturnsFalseAndLeavesUntouched()
    {
        await using var f = new Fixture();
        var owner = MakeUser("owner@test.com");
        var other = MakeUser("other@test.com");
        f.Ctx.Users.AddRange(owner, other);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.Add(MakeLink(owner.Id, "tok"));
        await f.Ctx.SaveChangesAsync();
        var linkId = (await f.Ctx.ShareLinks.FirstAsync(l => l.Token == "tok")).Id;

        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(other.Id);
        var biz = new ShareLinkBusiness(f.Ctx, userContext.Object);

        var result = await biz.DeactivateAsync(linkId);

        Assert.IsFalse(result);
        var inDb = await f.Ctx.ShareLinks.FindAsync(linkId);
        Assert.IsTrue(inDb!.IsActive, "Owner's link must remain active");
    }

    // ─── GetByTokenAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetByTokenAsync_ActiveNonExpiredLink_ReturnsDto()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        var link = MakeLink(user.Id, "tok-active");
        link.ExpiresAt = DateTime.UtcNow.AddDays(1);
        f.Ctx.ShareLinks.Add(link);
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetByTokenAsync("tok-active");

        Assert.IsNotNull(result);
        Assert.AreEqual("tok-active", result.Token);
    }

    [TestMethod]
    public async Task GetByTokenAsync_InactiveLink_ReturnsNull()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        var link = MakeLink(user.Id, "tok-inactive");
        link.IsActive = false;
        f.Ctx.ShareLinks.Add(link);
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetByTokenAsync("tok-inactive");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetByTokenAsync_ExpiredLink_ReturnsNull()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        var link = MakeLink(user.Id, "tok-expired");
        link.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        f.Ctx.ShareLinks.Add(link);
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetByTokenAsync("tok-expired");

        Assert.IsNull(result);
    }

    // ─── IncrementViewCountAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task IncrementViewCountAsync_IncrementsCounter()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.Add(MakeLink(user.Id, "tok-view"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.IncrementViewCountAsync("tok-view");

        var inDb = await f.Ctx.ShareLinks.AsNoTracking().FirstAsync(l => l.Token == "tok-view");
        Assert.AreEqual(1, inDb.ViewCount);
    }

    // ─── GetOwnerIdAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetOwnerIdAsync_KnownToken_ReturnsOwnerId()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.Add(MakeLink(user.Id, "tok-owner"));
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetOwnerIdAsync("tok-owner");

        Assert.AreEqual(user.Id, result);
    }

    [TestMethod]
    public async Task GetOwnerIdAsync_UnknownToken_ReturnsNull()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetOwnerIdAsync("no-such-token");

        Assert.IsNull(result);
    }

    // ─── DiscoverAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DiscoverAsync_SortsByContinent_ThenCountry_ThenPlaces()
    {
        // User A: 3 continents, 10 countries
        // User B: 3 continents, 12 countries
        // User C: 2 continents, 20 countries
        // Expected order: B (3 continents, 12 countries), A (3 continents, 10 countries), C (2 continents)
        await using var f = new Fixture();

        // Seed countries by region — unique IsoNumeric and IsoAlpha2 per entry
        var americas = Enumerable.Range(0, 10).Select(i => new Country { IsoNumeric = 1000 + i, IsoAlpha2 = $"A{i}", Flag = "🏳", Name = $"Am{i}", Region = "Americas" }).ToList();
        var europe = Enumerable.Range(0, 10).Select(i => new Country { IsoNumeric = 2000 + i, IsoAlpha2 = $"E{i}", Flag = "🏳", Name = $"Eu{i}", Region = "Europe" }).ToList();
        var asia = Enumerable.Range(0, 10).Select(i => new Country { IsoNumeric = 3000 + i, IsoAlpha2 = $"S{i}", Flag = "🏳", Name = $"As{i}", Region = "Asia" }).ToList();
        f.Ctx.Countries.AddRange(americas);
        f.Ctx.Countries.AddRange(europe);
        f.Ctx.Countries.AddRange(asia);

        var userA = MakeUser("a@test.com", "User A");
        var userB = MakeUser("b@test.com", "User B");
        var userC = MakeUser("c@test.com", "User C");
        f.Ctx.Users.AddRange(userA, userB, userC);
        await f.Ctx.SaveChangesAsync();

        // User A: Americas(0-4) + Europe(0-3) + Asia(0) = 10 countries, 3 continents
        foreach (var c in americas.Take(5).Concat(europe.Take(4)).Concat(asia.Take(1)))
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = userA.Id, CountryId = c.Id, IsVisited = true });

        // User B: Americas(0-4) + Europe(0-4) + Asia(0,1) = 12 countries, 3 continents
        foreach (var c in americas.Take(5).Concat(europe.Take(5)).Concat(asia.Take(2)))
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = userB.Id, CountryId = c.Id, IsVisited = true });

        // User C: Americas(0-9) + Europe(0-9) = 20 countries, 2 continents
        foreach (var c in americas.Concat(europe))
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = userC.Id, CountryId = c.Id, IsVisited = true });

        f.Ctx.ShareLinks.Add(MakeLink(userA.Id, "token-a"));
        f.Ctx.ShareLinks.Add(MakeLink(userB.Id, "token-b"));
        f.Ctx.ShareLinks.Add(MakeLink(userC.Id, "token-c"));
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("");

        Assert.HasCount(3, results);
        Assert.AreEqual("token-b", results[0].Token, "User B: 3 continents, 12 countries — must rank first");
        Assert.AreEqual("token-a", results[1].Token, "User A: 3 continents, 10 countries — must rank second");
        Assert.AreEqual("token-c", results[2].Token, "User C: 2 continents — must rank last");
    }

    [TestMethod]
    public async Task DiscoverAsync_ExcludesInactiveLinks()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();

        var inactive = MakeLink(user.Id, "token-inactive");
        inactive.IsActive = false;
        f.Ctx.ShareLinks.Add(inactive);
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("");

        Assert.HasCount(0, results);
    }

    [TestMethod]
    public async Task DiscoverAsync_ExcludesNonDiscoverableUsers()
    {
        await using var f = new Fixture();
        var user = MakeUser("u@test.com", isDiscoverable: false);
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();

        f.Ctx.ShareLinks.Add(MakeLink(user.Id, "token-nd"));
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("");

        Assert.HasCount(0, results);
    }

    [TestMethod]
    public async Task DiscoverAsync_FiltersBy_DisplayNameQuery()
    {
        await using var f = new Fixture();
        var alice = MakeUser("a@test.com", "Alice");
        var bob = MakeUser("b@test.com", "Bob");
        f.Ctx.Users.AddRange(alice, bob);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.AddRange(MakeLink(alice.Id, "tok-alice"), MakeLink(bob.Id, "tok-bob"));
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("Alice");

        Assert.HasCount(1, results);
        Assert.AreEqual("tok-alice", results[0].Token);
    }
}
