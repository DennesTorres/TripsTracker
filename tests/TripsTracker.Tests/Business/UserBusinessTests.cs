using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class UserBusinessTests
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
            "Database=TripsTracker_Test_Users");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
        // Insert a sentinel User with Id=0 so orphaned places (UserId=0, pre-migration state)
        // satisfy the FK constraint without disabling it.
        await ctx.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT Users ON;
            INSERT INTO Users (Id, Email, CreatedAt, IsDiscoverable)
            VALUES (0, 'orphan@system.local', GETUTCDATE(), 0);
            SET IDENTITY_INSERT Users OFF;");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public UserBusiness Biz { get; }
        public TripsTrackerDbContext Ctx { get; }
        private IDbContextTransaction? _transaction;

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
            Biz = new UserBusiness(Ctx);
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

    private static Country AnyCountry(string iso2) => new()
    {
        IsoNumeric = (short)((int)iso2[0] * 100 + (int)iso2[1]),
        IsoAlpha2 = iso2,
        Flag = "🏳",
        Name = $"Country {iso2}",
        Region = "Test",
    };

    private static User AnyUser(string email) => new()
    {
        Email = email,
        CreatedAt = DateTime.UtcNow,
    };

    private static Place OrphanedPlace(int countryId, string city) => new()
    {
        CountryId = countryId,
        City = city,
        UserId = 0,
        Lon = 0,
        Lat = 0,
    };

    #endregion

    // ─── UpdateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_SetsIsDiscoverable()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var user = AnyUser("u@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();

        var dto = new TripsTracker.Domain.UpdateUserDto(null, null, IsDiscoverable: true);
        var result = await f.Biz.UpdateAsync(user.Id, dto);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsDiscoverable, "IsDiscoverable must be set to true");
    }

    // ─── AdoptOrphanedPlacesAsync ─────────────────────────────────────────────

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_DoesNothing_WhenNoOrphanedPlaces()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = AnyCountry("BR");
        f.Ctx.Countries.Add(country);
        var user = AnyUser("user@example.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        // A place that already belongs to user (not orphaned)
        f.Ctx.Places.Add(new Place { CountryId = country.Id, City = "São Paulo", UserId = user.Id, Lon = 0, Lat = 0 });
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(user.Id);

        var userCountries = await f.Ctx.Set<UserCountry>().ToListAsync();
        Assert.HasCount(0, userCountries, "No UserCountry rows must be created when there are no orphaned places");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_AssignsOrphanedPlaceUserId_ToCallingUser()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = AnyCountry("BR");
        f.Ctx.Countries.Add(country);
        var user = AnyUser("user@example.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        var place = OrphanedPlace(country.Id, "São Paulo");
        f.Ctx.Places.Add(place);
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(user.Id);

        f.Ctx.ChangeTracker.Clear();
        var updated = await f.Ctx.Places.FindAsync(place.Id);
        Assert.AreEqual(user.Id, updated!.UserId, "Orphaned place must be re-assigned to the calling user");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_CreatesUserCountry_ForEachDistinctCountry()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var br = AnyCountry("BR");
        var ar = AnyCountry("AR");
        f.Ctx.Countries.AddRange(br, ar);
        var user = AnyUser("user@example.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Places.AddRange(
            OrphanedPlace(br.Id, "São Paulo"),
            OrphanedPlace(br.Id, "Rio"),     // same country as above
            OrphanedPlace(ar.Id, "Buenos Aires"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(user.Id);

        var userCountries = await f.Ctx.Set<UserCountry>().Where(uc => uc.UserId == user.Id).ToListAsync();
        Assert.HasCount(2, userCountries, "One UserCountry row per distinct country");
        Assert.IsTrue(userCountries.All(uc => uc.IsVisited), "All adopted countries must be marked as visited");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_SetsIsVisited_OnExistingUserCountry()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = AnyCountry("BR");
        f.Ctx.Countries.Add(country);
        var user = AnyUser("user@example.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        // User already has an unvisited UserCountry row for Brazil
        f.Ctx.Set<UserCountry>().Add(new UserCountry
            { UserId = user.Id, CountryId = country.Id, IsVisited = false, IsHome = false, ShowStateBorders = false });
        f.Ctx.Places.Add(OrphanedPlace(country.Id, "São Paulo"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(user.Id);

        var uc = await f.Ctx.Set<UserCountry>().FirstAsync(uc => uc.UserId == user.Id && uc.CountryId == country.Id);
        Assert.IsTrue(uc.IsVisited, "Existing UserCountry must be updated to IsVisited = true");
    }

    [TestMethod]
    public async Task AdoptOrphanedPlacesAsync_DoesNotDuplicate_ExistingUserCountry()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var country = AnyCountry("BR");
        f.Ctx.Countries.Add(country);
        var user = AnyUser("user@example.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<UserCountry>().Add(new UserCountry
            { UserId = user.Id, CountryId = country.Id, IsVisited = false, IsHome = false, ShowStateBorders = false });
        f.Ctx.Places.Add(OrphanedPlace(country.Id, "São Paulo"));
        await f.Ctx.SaveChangesAsync();

        await f.Biz.AdoptOrphanedPlacesAsync(user.Id);

        var count = await f.Ctx.Set<UserCountry>().CountAsync(uc => uc.UserId == user.Id && uc.CountryId == country.Id);
        Assert.AreEqual(1, count, "Must not create duplicate UserCountry rows");
    }
}
