using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;

namespace TripsTracker.Tests.Business;

[TestClass]
public class CountryBusinessTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public CountryBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private readonly SqliteConnection _conn;

        public Fixture(int userId = 1)
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();

            var ddl = new[]
            {
                """
                CREATE TABLE Countries (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    IsoNumeric INTEGER NOT NULL, IsoAlpha2 TEXT NOT NULL, IsoAlpha3 TEXT NULL,
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

            var userContext = new FakeUserContext(userId);
            Biz = new CountryBusiness(Ctx, userContext);
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }

    private sealed class FakeUserContext(int userId) : TripsTracker.Interfaces.IUserContext
    {
        public int? UserId => userId;
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private static Country CountryWithIso3(int id, string iso2, string iso3) => new()
    {
        Id = id,
        IsoNumeric = id * 10,
        IsoAlpha2 = iso2,
        IsoAlpha3 = iso3,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Europe",
    };

    private static Country CountryWithoutIso3(int id, string iso2) => new()
    {
        Id = id,
        IsoNumeric = id * 10,
        IsoAlpha2 = iso2,
        IsoAlpha3 = null,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Europe",
    };

    #endregion

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsIsoAlpha3_WhenCountryExists()
    {
        await using var f = new Fixture();
        f.Ctx.Set<Country>().Add(CountryWithIso3(1, "DE", "DEU"));
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetIsoAlpha3Async(1);

        Assert.AreEqual("DEU", result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenIsoAlpha3NotSet()
    {
        await using var f = new Fixture();
        f.Ctx.Set<Country>().Add(CountryWithoutIso3(1, "XX"));
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetIsoAlpha3Async(1);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetIsoAlpha3Async_ReturnsNull_WhenCountryNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetIsoAlpha3Async(999);

        Assert.IsNull(result);
    }
}
