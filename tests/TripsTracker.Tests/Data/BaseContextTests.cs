using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;

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
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    #endregion

    [TestMethod]
    public void BaseContext_CanBeInstantiated()
    {
        using var context = CreateContext();
        Assert.IsNotNull(context);
    }

    [TestMethod]
    public async Task BaseContext_CanAddAndRetrieveEntities()
    {
        using var context = CreateContext();
        context.TestEntities.Add(new TestEntity { Id = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var entity = await context.TestEntities.FindAsync(1);
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
