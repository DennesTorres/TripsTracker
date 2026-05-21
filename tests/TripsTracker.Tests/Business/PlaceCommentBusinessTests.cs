using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

file sealed class CommentTestUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;
    public CommentTestUserContext(int userId) { UserId = userId; Email = $"user{userId}@test.com"; }
}

[TestClass]
public class PlaceCommentBusinessTests
{
    #region Fixture

    private static DbContextOptions<TripsTrackerDbContext> _options = null!;
    private static int _placeId;
    private static int _place2Id;
    private static int _user1Id;
    private static int _user2Id;

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
            "Database=TripsTracker_Test_Comments");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();

        // Seed FK parents shared across all tests in this class
        var country = new Country { IsoNumeric = 9001, IsoAlpha2 = "ZZ", Flag = "🏳", Name = "TestCountry", Region = "Test" };
        ctx.Countries.Add(country);
        var u1 = new User { Email = "seed1@comments.test", CreatedAt = DateTime.UtcNow };
        var u2 = new User { Email = "seed2@comments.test", CreatedAt = DateTime.UtcNow };
        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var p1 = new Place { City = "SeedCity1", CountryId = country.Id, UserId = u1.Id, Lon = 0, Lat = 0 };
        var p2 = new Place { City = "SeedCity2", CountryId = country.Id, UserId = u1.Id, Lon = 0, Lat = 0 };
        ctx.Places.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        _placeId = p1.Id;
        _place2Id = p2.Id;
        _user1Id = u1.Id;
        _user2Id = u2.Id;
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
            Ctx.Database.OpenConnection(); // keep single connection enlisted — prevents DTC escalation
        }

        public PlaceCommentBusiness ForUser(int userId)
            => new PlaceCommentBusiness(Ctx, new CommentTestUserContext(userId));

        public async ValueTask DisposeAsync()
        {
            Ctx.Database.CloseConnection();
            await Ctx.DisposeAsync();
            _scope.Dispose(); // no Complete() → automatic rollback
        }
    }

    private static User MakeUser(string email, string? displayName = null) => new()
    {
        Email = email, DisplayName = displayName, CreatedAt = DateTime.UtcNow,
    };

    private static PlaceComment MakeComment(int placeId, int userId, string text) => new()
    {
        PlaceId = placeId, UserId = userId, Text = text, CreatedAt = DateTime.UtcNow,
    };

    #endregion

    // ─── GetByPlaceAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetByPlaceAsync_ReturnsComments_FromAllUsers()
    {
        await using var f = new Fixture();
        var alice = MakeUser("a@test.com", "Alice");
        var bob = MakeUser("b@test.com", "Bob");
        f.Ctx.Users.AddRange(alice, bob);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(_placeId, alice.Id, "Alice's comment"),
            MakeComment(_placeId, bob.Id, "Bob's comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(alice.Id).GetByPlaceAsync(_placeId);

        Assert.AreEqual(2, comments.Count, "Comments from all users should be returned");
    }

    [TestMethod]
    public async Task GetByPlaceAsync_OnlyReturnsSamePlaceComments()
    {
        await using var f = new Fixture();
        var user = MakeUser("a@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(_placeId, user.Id, "Place 1 comment"),
            MakeComment(_place2Id, user.Id, "Place 2 comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(user.Id).GetByPlaceAsync(_placeId);

        Assert.AreEqual(1, comments.Count);
        Assert.AreEqual(_placeId, comments[0].PlaceId);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenCommentOwnedByOtherUser()
    {
        await using var f = new Fixture();
        var comment = MakeComment(_placeId, _user2Id, "Other user's comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(_user1Id).DeleteAsync(comment.Id);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_WhenCommentIsOwn()
    {
        await using var f = new Fixture();
        var comment = MakeComment(_placeId, _user1Id, "My comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(_user1Id).DeleteAsync(comment.Id);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    // ─── VoteAsync ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task VoteAsync_CreatesUpvote_WhenNoneExists()
    {
        await using var f = new Fixture();
        var comment = MakeComment(_placeId, _user2Id, "A comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).VoteAsync(comment.Id, isUpvote: true);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == _user1Id && r.CommentId == comment.Id);
        Assert.IsTrue(vote.IsUpvote);
    }

    [TestMethod]
    public async Task VoteAsync_UpdatesVote_WhenAlreadyVoted()
    {
        await using var f = new Fixture();
        var comment = MakeComment(_placeId, _user2Id, "A comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<CommentRating>().Add(new CommentRating { UserId = _user1Id, CommentId = comment.Id, IsUpvote = true, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(_user1Id).VoteAsync(comment.Id, isUpvote: false);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == _user1Id && r.CommentId == comment.Id);
        Assert.IsFalse(vote.IsUpvote);
        Assert.AreEqual(1, await f.Ctx.Set<CommentRating>().CountAsync());
    }
}
