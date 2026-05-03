using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;

namespace TripsTracker.Tests.Business;

[TestClass]
public class UserBusinessTests
{
    #region Fixture

    /// <summary>
    /// SQLite in-memory fixture for UserBusiness tests.
    /// EF Core's EnsureCreated() skips the VisitedStates view (ToView mapping).
    /// The HasDefaultValueSql("GETUTCDATE()") is stored in the DDL but never evaluated
    /// because all tests provide CreatedAt explicitly.
    /// </summary>
    private sealed class Fixture : IAsyncDisposable
    {
        public UserBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            // Create schema manually without FK constraints so that UserId=0 orphaned places
            // (which have no matching user row) can be inserted — mirroring the production state
            // before the multi-user migration added the FK column.
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
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    IsDiscoverable INTEGER NOT NULL DEFAULT 0,
                    StorageUsedBytes INTEGER NOT NULL DEFAULT 0,
                    TotalPoints INTEGER NOT NULL DEFAULT 0
                )
                """,
                "CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)",
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
                """
                CREATE TABLE UserCountries (
                    UserId INTEGER NOT NULL, CountryId INTEGER NOT NULL,
                    IsHome INTEGER NOT NULL DEFAULT 0, IsVisited INTEGER NOT NULL DEFAULT 0,
                    ShowStateBorders INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (UserId, CountryId)
                )
                """,
                "CREATE INDEX IX_UserCountries_UserId ON UserCountries (UserId)",
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
            // EnsureCreated() not called — schema built manually above without FK constraints
            Biz = new UserBusiness(Ctx);
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }

    private static Country AnyCountry(int id, string iso2) => new()
    {
        Id = id,
        IsoNumeric = id * 10,
        IsoAlpha2 = iso2,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Test",
    };

    private static User AnyUser(int id, string email) => new()
    {
        Id = id,
        Email = email,
        CreatedAt = DateTime.UtcNow,
    };

    private static Place OrphanedPlace(int id, int countryId, string city) => new()
    {
        Id = id,
        CountryId = countryId,
        City = city,
        UserId = 0,
        Lon = 0,
        Lat = 0,
    };

    #endregion

    // ─── AdoptOrphanedPlacesAsync ─────────────────────────────────────────────

    // ─── UpdateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_SetsIsDiscoverable()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(AnyUser(1, "u@test.com"));
        await f.Ctx.SaveChangesAsync();

        var dto = new TripsTracker.Domain.UpdateUserDto(null, null, IsDiscoverable: true);
        var result = await f.Biz.UpdateAsync(1, dto);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsDiscoverable, "IsDiscoverable must be set to true");
    }

    // ─── AdoptOrphanedPlacesAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_DoesNothing_WhenNoOrphanedPlaces()
    {
        await using var f = new Fixture();
        f.Ctx.Countries.Add(AnyCountry(1, "BR"));
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        // A place that already belongs to user 1 (not orphaned)
        f.Ctx.Places.Add(new Place { Id = 1, CountryId = 1, City = "São Paulo", UserId = 1, Lon = 0, Lat = 0 });
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(1);

        var userCountries = await f.Ctx.Set<UserCountry>().ToListAsync();
        Assert.HasCount(0, userCountries, "No UserCountry rows must be created when there are no orphaned places");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_AssignsOrphanedPlaceUserId_ToCallingUser()
    {
        await using var f = new Fixture();
        f.Ctx.Countries.Add(AnyCountry(1, "BR"));
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        f.Ctx.Places.Add(OrphanedPlace(1, 1, "São Paulo"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(1);

        f.Ctx.ChangeTracker.Clear();
        var place = await f.Ctx.Places.FindAsync(1);
        Assert.AreEqual(1, place!.UserId, "Orphaned place must be re-assigned to the calling user");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_CreatesUserCountry_ForEachDistinctCountry()
    {
        await using var f = new Fixture();
        f.Ctx.Countries.AddRange(AnyCountry(1, "BR"), AnyCountry(2, "AR"));
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        f.Ctx.Places.AddRange(
            OrphanedPlace(1, 1, "São Paulo"),
            OrphanedPlace(2, 1, "Rio"),     // same country as above
            OrphanedPlace(3, 2, "Buenos Aires"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(1);

        var userCountries = await f.Ctx.Set<UserCountry>().Where(uc => uc.UserId == 1).ToListAsync();
        Assert.HasCount(2, userCountries, "One UserCountry row per distinct country");
        Assert.IsTrue(userCountries.All(uc => uc.IsVisited), "All adopted countries must be marked as visited");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_SetsIsVisited_OnExistingUserCountry()
    {
        await using var f = new Fixture();
        f.Ctx.Countries.Add(AnyCountry(1, "BR"));
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        // User already has an unvisited UserCountry row for Brazil
        f.Ctx.Set<UserCountry>().Add(new UserCountry
            { UserId = 1, CountryId = 1, IsVisited = false, IsHome = false, ShowStateBorders = false });
        f.Ctx.Places.Add(OrphanedPlace(1, 1, "São Paulo"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(1);

        var uc = await f.Ctx.Set<UserCountry>().FirstAsync(uc => uc.UserId == 1 && uc.CountryId == 1);
        Assert.IsTrue(uc.IsVisited, "Existing UserCountry must be updated to IsVisited = true");
    }

    // ─── Storage quota ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetStorageUsedAsync_ReturnsZero_ForNewUser()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        await f.Ctx.SaveChangesAsync();

        var used = await f.Biz.GetStorageUsedAsync(1);

        Assert.AreEqual(0L, used);
    }

    [TestMethod]
    public async Task AddStorageUsedAsync_IncrementsStorageUsedBytes()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AddStorageUsedAsync(1, 5_000_000);

        var user = await f.Ctx.Users.AsNoTracking().FirstAsync(u => u.Id == 1);
        Assert.AreEqual(5_000_000L, user.StorageUsedBytes);
    }

    [TestMethod]
    public async Task AddStorageUsedAsync_AccumulatesAndDecrements()
    {
        await using var f = new Fixture();
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AddStorageUsedAsync(1, 10_000_000);
        await f.Biz.AddStorageUsedAsync(1, -3_000_000);

        var user = await f.Ctx.Users.AsNoTracking().FirstAsync(u => u.Id == 1);
        Assert.AreEqual(7_000_000L, user.StorageUsedBytes);
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_DoesNotDuplicate_ExistingUserCountry()
    {
        await using var f = new Fixture();
        f.Ctx.Countries.Add(AnyCountry(1, "BR"));
        f.Ctx.Users.Add(AnyUser(1, "user@example.com"));
        f.Ctx.Set<UserCountry>().Add(new UserCountry
            { UserId = 1, CountryId = 1, IsVisited = false, IsHome = false, ShowStateBorders = false });
        f.Ctx.Places.Add(OrphanedPlace(1, 1, "São Paulo"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(1);

        var count = await f.Ctx.Set<UserCountry>().CountAsync(uc => uc.UserId == 1 && uc.CountryId == 1);
        Assert.AreEqual(1, count, "Must not create duplicate UserCountry rows");
    }
}
