using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TripsTracker.Data;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Data;

[TestClass]
public class BaseContextTests
{
    #region Test Helpers

    private class TestEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private class TestDbContext : BaseContext<TestDbContext>
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntity>().Property(t => t.Id).ValueGeneratedNever();
        }
    }

    private static DbContextOptions<TestDbContext> _options = null!;

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
            "Database=TripsTracker_Test_BaseContext");

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TestDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await using var ctx = new TestDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TestDbContext Ctx { get; }
        private IDbContextTransaction? _transaction;

        public Fixture()
        {
            Ctx = new TestDbContext(_options);
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

    #endregion

    [TestMethod]
    public void BaseContext_CanBeInstantiated()
    {
        using var context = new TestDbContext(_options);
        Assert.IsNotNull(context);
    }

    [TestMethod]
    public async Task BaseContext_CanAddAndRetrieveEntities()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        f.Ctx.TestEntities.Add(new TestEntity { Id = 1, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        var entity = await f.Ctx.TestEntities.FindAsync(1);
        Assert.IsNotNull(entity);
        Assert.AreEqual(1, entity.Id);
    }

    [TestMethod]
    public void BaseContext_IsAbstractInDesign()
    {
        // BaseContext<T> itself is abstract — only subclasses are instantiated
        Assert.IsTrue(typeof(BaseContext<>).IsAbstract);
    }

    [TestMethod]
    public void BaseContext_InheritsFromDbContext()
    {
        Assert.IsTrue(typeof(BaseContext<>).BaseType == typeof(DbContext));
    }
}
