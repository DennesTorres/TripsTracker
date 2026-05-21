using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class StorageBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _userId;

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
            "Database=TripsTracker_Test_Storage");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var user = new User { Email = "seed@storage.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        _userId = user.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly TransactionScope _scope;

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public StorageBusiness Build() => new(Ctx);

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    [TestMethod]
    public async Task GetUsageAsync_ReturnsStoredUsage_And10GbLimit()
    {
        await using var f = new Fixture();
        await f.Ctx.Users
            .Where(u => u.Id == _userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.StorageUsedBytes, 500_000L));

        var result = await f.Build().GetUsageAsync(_userId);

        Assert.AreEqual(500_000L, result.UsedBytes);
        Assert.AreEqual(10L * 1024 * 1024 * 1024, result.LimitBytes);
    }

    [TestMethod]
    public async Task UpdateStorageAsync_PersistsUsedBytesAndRefreshedAt()
    {
        await using var f = new Fixture();
        var now = DateTime.UtcNow;

        await f.Build().UpdateStorageAsync(_userId, 750_000L, now);

        var user = await f.Ctx.Users.AsNoTracking().FirstAsync(u => u.Id == _userId);
        Assert.AreEqual(750_000L, user.StorageUsedBytes);
        Assert.IsNotNull(user.StorageLastRefreshedAt);
        Assert.IsTrue(Math.Abs((user.StorageLastRefreshedAt!.Value - now).TotalMilliseconds) < 1000);
    }
}
