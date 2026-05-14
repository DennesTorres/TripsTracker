using Microsoft.EntityFrameworkCore;
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
    public static void ClassInitialize(TestContext _)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .Build();

        var dbOpts = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(dbOpts.ConnectionString)
            .Options;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        public UserBusiness Biz { get; }
        private readonly List<int> _userIds = [];

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
            Biz = new UserBusiness(Ctx);
        }

        public async Task<User> AddUserAsync(string email, string? displayName = null)
        {
            var user = new User { Email = email, DisplayName = displayName };
            Ctx.Set<User>().Add(user);
            await Ctx.SaveChangesAsync();
            _userIds.Add(user.Id);
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            if (_userIds.Count > 0)
                await Ctx.Set<User>()
                    .Where(u => _userIds.Contains(u.Id))
                    .ExecuteDeleteAsync();
            await Ctx.DisposeAsync();
        }
    }

    #endregion

    [TestMethod]
    public async Task GetByEmailAsync_ReturnsUser_WhenEmailExists()
    {
        await using var f = new Fixture();
        var user = await f.AddUserAsync("getbyemail@test.local", "Test User");

        var result = await f.Biz.GetByEmailAsync("getbyemail@test.local");

        Assert.IsNotNull(result);
        Assert.AreEqual(user.Id, result.Id);
        Assert.AreEqual("Test User", result.DisplayName);
    }

    [TestMethod]
    public async Task GetByEmailAsync_ReturnsNull_WhenEmailNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.GetByEmailAsync("nonexistent@test.local");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreateAsync_ReturnsUserDto_WithCorrectValues()
    {
        await using var f = new Fixture();

        var result = await f.Biz.CreateAsync("created@test.local", "Created User");

        Assert.IsNotNull(result);
        Assert.AreEqual("created@test.local", result.Email);
        Assert.AreEqual("Created User", result.DisplayName);
        Assert.IsTrue(result.Id > 0);
        f.Ctx.Set<User>().Remove(f.Ctx.Set<User>().Find(result.Id)!);
        await f.Ctx.SaveChangesAsync();
    }

    [TestMethod]
    public async Task CreateAsync_WorksWithNullDisplayName()
    {
        await using var f = new Fixture();

        var result = await f.Biz.CreateAsync("nodisplay@test.local", null);

        Assert.IsNotNull(result);
        Assert.IsNull(result.DisplayName);
        f.Ctx.Set<User>().Remove(f.Ctx.Set<User>().Find(result.Id)!);
        await f.Ctx.SaveChangesAsync();
    }

    [TestMethod]
    public async Task UpdateAsync_UpdatesDisplayName()
    {
        await using var f = new Fixture();
        var user = await f.AddUserAsync("update@test.local", "OldName");

        var result = await f.Biz.UpdateAsync(user.Id, new TripsTracker.Domain.UpdateUserDto("NewName", null));

        Assert.IsNotNull(result);
        Assert.AreEqual("NewName", result.DisplayName);
    }

    [TestMethod]
    public async Task UpdateAsync_ReturnsNull_WhenUserNotFound()
    {
        await using var f = new Fixture();

        var result = await f.Biz.UpdateAsync(int.MaxValue, new TripsTracker.Domain.UpdateUserDto("Name", null));

        Assert.IsNull(result);
    }
}
