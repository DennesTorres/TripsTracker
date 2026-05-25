using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Process;

namespace TripsTracker.Tests.Process;

[TestClass]
[DoNotParallelize]
public class CommentProcessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _placeId;
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
            "Database=TripsTracker_Test_CommentProcess7");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        var user = new User { Email = "comment7@test.com", CreatedAt = DateTime.UtcNow };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        _userId = user.Id;

        var country = new Country { IsoNumeric = 76, IsoAlpha2 = "BR", Flag = "\U0001F1E7\U0001F1F7", Name = "Brazil", Region = "Americas" };
        ctx.Set<Country>().Add(country);
        await ctx.SaveChangesAsync();

        var place = new Place
        {
            Lon = -46.63, Lat = -23.55, CountryId = country.Id,
            City = "São Paulo", StateAbbr = "SP", StateName = "São Paulo",
            IsHome = false, UserId = _userId
        };
        ctx.Set<Place>().Add(place);
        await ctx.SaveChangesAsync();
        _placeId = place.Id;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TransactionScope _scope;
        public TripsTrackerDbContext Ctx { get; }

        public Fixture()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Ctx = new TripsTrackerDbContext(_options);
            Ctx.Database.OpenConnection();
        }

        public CommentProcess Build()
        {
            var userCtx = new TestUserContext(_userId);
            var comments = new PlaceCommentBusiness(Ctx, userCtx);
            var points = new PointsBusiness(Ctx, userCtx);
            return new CommentProcess(comments, points);
        }

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose();
        }
    }

    #endregion

    // ─── CreateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_CreatesComment_AndAwards3Points()
    {
        await using var f = new Fixture();
        var sut = f.Build();

        var result = await sut.CreateAsync(_placeId, "Test comment text");

        // Comment persisted with correct owner
        Assert.AreEqual(_placeId, result.PlaceId);
        Assert.AreEqual(_userId, result.UserId);
        Assert.AreEqual("Test comment text", result.Text);

        // PointEvent created with correct fields
        var evt = await f.Ctx.Set<PointEvent>().AsNoTracking()
            .FirstOrDefaultAsync(e => e.ReferenceId == result.Id && e.ReferenceType == "Comment");
        Assert.IsNotNull(evt, "A PointEvent must be created for the comment.");
        Assert.AreEqual("comment_added", evt.EventType);
        Assert.AreEqual(3, evt.Points);
        Assert.AreEqual(_userId, evt.UserId);

        // User.TotalPoints incremented
        var user = await f.Ctx.Set<User>().AsNoTracking().FirstAsync(u => u.Id == _userId);
        Assert.AreEqual(3, user.TotalPoints);
    }
}
