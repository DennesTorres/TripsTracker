using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;

namespace TripsTracker.Tests.Data;

[TestClass]
public class BusinessBaseTests
{
    #region Test Helpers

    private class TripEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    private record TripDomain(int Id, string Name);

    private class TestDbContext : BaseContext<TestDbContext>
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        public DbSet<TripEntity> Trips => Set<TripEntity>();
    }

    private class TripBusiness : BusinessBase<TripEntity>
    {
        public TripBusiness(TestDbContext context) : base(context) { }

        protected override IQueryable<TripEntity> BuildBaseQuery()
            => base.BuildBaseQuery().Where(t => !t.IsDeleted);

        public Task InsertTripAsync(TripEntity entity, CancellationToken ct = default)
            => InsertAsync(entity, ct);

        public Task<TripDomain?> GetTripByIdAsync(int id, CancellationToken ct = default)
            => BuildBaseQuery()
                .Where(t => t.Id == id)
                .Select(t => new TripDomain(t.Id, t.Name))
                .FirstOrDefaultAsync(ct);

        public Task<List<TripDomain>> GetAllTripsAsync(CancellationToken ct = default)
            => BuildBaseQuery().Select(t => new TripDomain(t.Id, t.Name)).ToListAsync(ct);

        public Task<int> UpdateTripNameAsync(int id, string newName, CancellationToken ct = default)
            => ExecuteUpdateAsync(t => t.Id == id, s => s.SetProperty(t => t.Name, newName), ct);

        public Task<int> HardDeleteTripAsync(int id, CancellationToken ct = default)
            => ExecuteDeleteAsync(t => t.Id == id, ct);

        public Task<TripDomain?> FindByNameAsync(string name, CancellationToken ct = default)
            => BuildBaseQuery()
                .Where(t => t.Name == name)
                .Select(t => new TripDomain(t.Id, t.Name))
                .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// SQLite in-memory fixture — supports ExecuteUpdate/ExecuteDelete unlike the EF InMemory provider.
    /// </summary>
    private sealed class Fixture : IAsyncDisposable
    {
        public TripBusiness Biz { get; }
        public TestDbContext Ctx { get; }
        private readonly SqliteConnection _conn;

        public Fixture()
        {
            _conn = new SqliteConnection("Data Source=:memory:");
            _conn.Open();
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_conn)
                .Options;
            Ctx = new TestDbContext(options);
            Ctx.Database.EnsureCreated();
            Biz = new TripBusiness(Ctx);
        }

        public async ValueTask DisposeAsync()
        {
            await Ctx.DisposeAsync();
            await _conn.DisposeAsync();
        }
    }

    #endregion

    #region InsertAsync

    [TestMethod]
    public async Task InsertAsync_SavesEntityToDatabase()
    {
        await using var f = new Fixture();

        await f.Biz.InsertTripAsync(new TripEntity { Id = 1, Name = "Paris" });

        var saved = await f.Ctx.Trips.FindAsync(1);
        Assert.IsNotNull(saved);
        Assert.AreEqual("Paris", saved.Name);
    }

    [TestMethod]
    public async Task InsertAsync_ContextStateIsCleanAfterSave()
    {
        await using var f = new Fixture();

        await f.Biz.InsertTripAsync(new TripEntity { Id = 1, Name = "Paris" });

        Assert.AreEqual(0, f.Ctx.ChangeTracker.Entries().Count(e =>
            e.State != EntityState.Unchanged && e.State != EntityState.Detached));
    }

    #endregion

    #region GetTripByIdAsync

    [TestMethod]
    public async Task GetByIdAsync_ReturnsMatchingDomainProjection()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.Add(new TripEntity { Id = 1, Name = "Rome" });
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetTripByIdAsync(1);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("Rome", result.Name);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetTripByIdAsync(99);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetByIdAsync_ExcludesSoftDeletedEntities()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.Add(new TripEntity { Id = 1, Name = "Deleted Trip", IsDeleted = true });
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.GetTripByIdAsync(1);

        Assert.IsNull(result, "Soft-deleted trip should not be returned.");
    }

    #endregion

    #region ExecuteUpdateAsync

    [TestMethod]
    public async Task ExecuteUpdateAsync_UpdatesFieldsWithoutLoadingRecord()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.Add(new TripEntity { Id = 1, Name = "Old Name" });
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ChangeTracker.Clear();

        var affected = await f.Biz.UpdateTripNameAsync(1, "New Name");

        Assert.AreEqual(1, affected);
        var entity = await f.Ctx.Trips.FindAsync(1);
        Assert.AreEqual("New Name", entity!.Name);
    }

    [TestMethod]
    public async Task ExecuteUpdateAsync_ReturnsZero_WhenNoMatchingRows()
    {
        await using var f = new Fixture();

        var affected = await f.Biz.UpdateTripNameAsync(99, "New Name");

        Assert.AreEqual(0, affected);
    }

    #endregion

    #region ExecuteDeleteAsync

    [TestMethod]
    public async Task ExecuteDeleteAsync_DeletesMatchingRows()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.Add(new TripEntity { Id = 1, Name = "To Delete" });
        await f.Ctx.SaveChangesAsync();
        f.Ctx.ChangeTracker.Clear();

        var affected = await f.Biz.HardDeleteTripAsync(1);

        Assert.AreEqual(1, affected);
        var entity = await f.Ctx.Trips.FindAsync(1);
        Assert.IsNull(entity);
    }

    [TestMethod]
    public async Task ExecuteDeleteAsync_ReturnsZero_WhenNoMatchingRows()
    {
        await using var f = new Fixture();

        var affected = await f.Biz.HardDeleteTripAsync(99);

        Assert.AreEqual(0, affected);
    }

    #endregion

    #region BuildBaseQuery

    [TestMethod]
    public async Task BuildBaseQuery_ExcludesSoftDeletedEntities()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.AddRange(
            new TripEntity { Id = 1, Name = "Active", IsDeleted = false },
            new TripEntity { Id = 2, Name = "Deleted", IsDeleted = true });
        await f.Ctx.SaveChangesAsync();

        var results = await f.Biz.GetAllTripsAsync();

        Assert.HasCount(1, results);
        Assert.AreEqual("Active", results[0].Name);
    }

    [TestMethod]
    public async Task BuildBaseQuery_WithFilter_ReturnsMatchingDomain()
    {
        await using var f = new Fixture();
        f.Ctx.Trips.AddRange(
            new TripEntity { Id = 1, Name = "Berlin" },
            new TripEntity { Id = 2, Name = "Tokyo" });
        await f.Ctx.SaveChangesAsync();

        var result = await f.Biz.FindByNameAsync("Berlin");

        Assert.IsNotNull(result);
        Assert.AreEqual("Berlin", result.Name);
    }

    #endregion
}
