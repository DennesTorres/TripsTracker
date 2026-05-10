using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Moq;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Business;

[TestClass]
public class PlaceCommentBusinessTests
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
            "Database=TripsTracker_Test_Comments");

        _options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();
        await ctx.Database.ExecuteSqlRawAsync(
            "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await using var ctx = new TripsTrackerDbContext(_options);
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Ctx { get; }
        private readonly Mock<IUserContext> _userContextMock = new();
        private IDbContextTransaction? _transaction;

        public Fixture()
        {
            Ctx = new TripsTrackerDbContext(_options);
        }

        public PlaceCommentBusiness ForUser(int userId)
        {
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            return new PlaceCommentBusiness(Ctx, _userContextMock.Object);
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
        await f.BeginTransactionAsync();
        var alice = MakeUser("a@test.com", "Alice");
        var bob = MakeUser("b@test.com", "Bob");
        f.Ctx.Users.AddRange(alice, bob);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(1, alice.Id, "Alice's comment"),
            MakeComment(1, bob.Id, "Bob's comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(alice.Id).GetByPlaceAsync(1);

        Assert.AreEqual(2, comments.Count, "Comments from all users should be returned");
    }

    [TestMethod]
    public async Task GetByPlaceAsync_OnlyReturnsSamePlaceComments()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var user = MakeUser("a@test.com");
        f.Ctx.Users.Add(user);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<PlaceComment>().AddRange(
            MakeComment(1, user.Id, "Place 1 comment"),
            MakeComment(2, user.Id, "Place 2 comment")
        );
        await f.Ctx.SaveChangesAsync();

        var comments = await f.ForUser(user.Id).GetByPlaceAsync(1);

        Assert.AreEqual(1, comments.Count);
        Assert.AreEqual(1, comments[0].PlaceId);
    }

    // ─── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_ReturnsFalse_WhenCommentOwnedByOtherUser()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var comment = MakeComment(1, 2, "Other user's comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(comment.Id);

        Assert.IsFalse(deleted);
        Assert.AreEqual(1, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    [TestMethod]
    public async Task DeleteAsync_ReturnsTrue_WhenCommentIsOwn()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var comment = MakeComment(1, 1, "My comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        var deleted = await f.ForUser(1).DeleteAsync(comment.Id);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await f.Ctx.Set<PlaceComment>().CountAsync());
    }

    // ─── VoteAsync ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task VoteAsync_CreatesUpvote_WhenNoneExists()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var comment = MakeComment(1, 2, "A comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).VoteAsync(comment.Id, isUpvote: true);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == 1 && r.CommentId == comment.Id);
        Assert.IsTrue(vote.IsUpvote);
    }

    [TestMethod]
    public async Task VoteAsync_UpdatesVote_WhenAlreadyVoted()
    {
        await using var f = new Fixture();
        await f.BeginTransactionAsync();
        var comment = MakeComment(1, 2, "A comment");
        f.Ctx.Set<PlaceComment>().Add(comment);
        await f.Ctx.SaveChangesAsync();
        f.Ctx.Set<CommentRating>().Add(new CommentRating { UserId = 1, CommentId = comment.Id, IsUpvote = true, CreatedAt = DateTime.UtcNow });
        await f.Ctx.SaveChangesAsync();

        await f.ForUser(1).VoteAsync(comment.Id, isUpvote: false);

        var vote = await f.Ctx.Set<CommentRating>().FirstAsync(r => r.UserId == 1 && r.CommentId == comment.Id);
        Assert.IsFalse(vote.IsUpvote);
        Assert.AreEqual(1, await f.Ctx.Set<CommentRating>().CountAsync());
    }
}
