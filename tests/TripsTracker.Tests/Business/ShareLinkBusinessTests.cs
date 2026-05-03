using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;

namespace TripsTracker.Tests.Business;

[TestClass]
public class ShareLinkBusinessTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public ShareLinkBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            // Create schema manually: same structure as TripsTrackerDbContext but without FK
            // constraints so we avoid GETUTCDATE() evaluation and FK ordering issues.
            var ddl = new[]
            {
                """
                CREATE TABLE Countries (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    IsoNumeric INTEGER NOT NULL, IsoAlpha2 TEXT NOT NULL,
                    Flag TEXT NOT NULL, Name TEXT NOT NULL, Region TEXT NOT NULL
                )
                """,
                "CREATE UNIQUE INDEX IX_Countries_IsoNumeric ON Countries (IsoNumeric)",
                """
                CREATE TABLE Users (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL, DisplayName TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01'
                )
                """,
                "CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)",
                """
                CREATE TABLE ShareLinks (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL, Token TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    RequiresLogin INTEGER NOT NULL DEFAULT 0,
                    IsDiscoverable INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    ExpiresAt TEXT,
                    ViewCount INTEGER NOT NULL DEFAULT 0
                )
                """,
                "CREATE UNIQUE INDEX IX_ShareLinks_Token ON ShareLinks (Token)",
                "CREATE INDEX IX_ShareLinks_UserId ON ShareLinks (UserId)",
                """
                CREATE TABLE UserCountries (
                    UserId INTEGER NOT NULL, CountryId INTEGER NOT NULL,
                    IsHome INTEGER NOT NULL DEFAULT 0,
                    IsVisited INTEGER NOT NULL DEFAULT 0,
                    ShowStateBorders INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (UserId, CountryId)
                )
                """,
                "CREATE INDEX IX_UserCountries_UserId ON UserCountries (UserId)",
                """
                CREATE TABLE Places (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    Lon REAL NOT NULL, Lat REAL NOT NULL, CountryId INTEGER NOT NULL,
                    City TEXT NOT NULL, StateAbbr TEXT, StateName TEXT,
                    IsHome INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL DEFAULT 0
                )
                """,
                "CREATE INDEX IX_Places_CountryId ON Places (CountryId)",
                "CREATE INDEX IX_Places_UserId ON Places (UserId)",
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

            var userContext = new Mock<IUserContext>();
            userContext.Setup(u => u.UserId).Returns((int?)null);
            Biz = new ShareLinkBusiness(Ctx, userContext.Object);
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

    private static Country MakeCountry(int id, string region) => new()
    {
        Id = id, IsoNumeric = id, IsoAlpha2 = $"C{id:D2}",
        Flag = "🏳", Name = $"Country{id}", Region = region,
    };

    private static ShareLink MakeLink(int id, int userId, string token) => new()
    {
        Id = id, UserId = userId, Token = token,
        IsActive = true, IsDiscoverable = true, RequiresLogin = false,
        CreatedAt = DateTime.UtcNow,
    };

    #endregion

    // ─── DiscoverAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DiscoverAsync_SortsByContinent_ThenCountry_ThenPlaces()
    {
        // User A: 3 continents, 10 countries
        // User B: 3 continents, 12 countries
        // User C: 2 continents, 20 countries
        // Expected order: B (3 continents, 12 countries), A (3 continents, 10 countries), C (2 continents)
        await using var f = new Fixture();

        // Seed countries: 3 regions × enough countries
        // Regions: "Americas", "Europe", "Asia"
        int cId = 1;
        // Americas: 1-10
        for (int i = 0; i < 10; i++) f.Ctx.Countries.Add(MakeCountry(cId++, "Americas"));
        // Europe: 11-20
        for (int i = 0; i < 10; i++) f.Ctx.Countries.Add(MakeCountry(cId++, "Europe"));
        // Asia: 21-30
        for (int i = 0; i < 10; i++) f.Ctx.Countries.Add(MakeCountry(cId++, "Asia"));

        // Users
        f.Ctx.Users.Add(MakeUser(1, "a@test.com", "User A"));
        f.Ctx.Users.Add(MakeUser(2, "b@test.com", "User B"));
        f.Ctx.Users.Add(MakeUser(3, "c@test.com", "User C"));
        await f.Ctx.SaveChangesAsync();

        // User A: Americas(1-5) + Europe(11-14) + Asia(21) = 10 countries, 3 continents
        foreach (var id in new[] { 1, 2, 3, 4, 5, 11, 12, 13, 14, 21 })
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = 1, CountryId = id, IsVisited = true });

        // User B: Americas(1-5) + Europe(11-15) + Asia(21,22) = 12 countries, 3 continents
        foreach (var id in new[] { 1, 2, 3, 4, 5, 11, 12, 13, 14, 15, 21, 22 })
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = 2, CountryId = id, IsVisited = true });

        // User C: Americas(1-10) + Europe(11-20) = 20 countries, 2 continents
        foreach (var id in Enumerable.Range(1, 10).Concat(Enumerable.Range(11, 10)))
            f.Ctx.Set<UserCountry>().Add(new UserCountry { UserId = 3, CountryId = id, IsVisited = true });

        // Share links
        f.Ctx.ShareLinks.Add(MakeLink(1, 1, "token-a"));
        f.Ctx.ShareLinks.Add(MakeLink(2, 2, "token-b"));
        f.Ctx.ShareLinks.Add(MakeLink(3, 3, "token-c"));
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
        f.Ctx.Users.Add(MakeUser(1, "u@test.com"));
        f.Ctx.Countries.Add(MakeCountry(1, "Europe"));
        await f.Ctx.SaveChangesAsync();

        var inactive = MakeLink(1, 1, "token-inactive");
        inactive.IsActive = false;
        f.Ctx.ShareLinks.Add(inactive);
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("");

        Assert.HasCount(0, results);
    }

    [TestMethod]
    public async Task DiscoverAsync_ExcludesNonDiscoverableLinks()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(MakeUser(1, "u@test.com"));
        f.Ctx.Countries.Add(MakeCountry(1, "Europe"));
        await f.Ctx.SaveChangesAsync();

        var link = MakeLink(1, 1, "token-nd");
        link.IsDiscoverable = false;
        f.Ctx.ShareLinks.Add(link);
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("");

        Assert.HasCount(0, results);
    }

    [TestMethod]
    public async Task DiscoverAsync_FiltersBy_DisplayNameQuery()
    {
        await using var f = new Fixture();
        f.Ctx.Users.AddRange(MakeUser(1, "a@test.com", "Alice"), MakeUser(2, "b@test.com", "Bob"));
        f.Ctx.Countries.Add(MakeCountry(1, "Europe"));
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ShareLinks.AddRange(MakeLink(1, 1, "tok-alice"), MakeLink(2, 2, "tok-bob"));
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.DiscoverAsync("Alice");

        Assert.HasCount(1, results);
        Assert.AreEqual("tok-alice", results[0].Token);
    }
}
