using Microsoft.EntityFrameworkCore;
using TripsTracker.Data.Entities;

namespace TripsTracker.Data;

public class TripsTrackerDbContext : BaseContext<TripsTrackerDbContext>
{
    public TripsTrackerDbContext(DbContextOptions<TripsTrackerDbContext> options) : base(options) { }

    public DbSet<Place> Places => Set<Place>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<VisitedState> VisitedStates => Set<VisitedState>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCountry> UserCountries => Set<UserCountry>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<PlacePhoto> PlacePhotos => Set<PlacePhoto>();
    public DbSet<PlaceComment> PlaceComments => Set<PlaceComment>();
    public DbSet<PhotoRating> PhotoRatings => Set<PhotoRating>();
    public DbSet<CommentRating> CommentRatings => Set<CommentRating>();
    public DbSet<PointEvent> PointEvents => Set<PointEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Place>(e =>
        {
            e.HasIndex(p => p.CountryId);
            e.HasIndex(p => p.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Country>().WithMany().HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Country>(e =>
        {
            e.HasIndex(c => c.IsoNumeric).IsUnique();
            e.HasIndex(c => c.Name);
        });

        modelBuilder.Entity<VisitedState>(e =>
        {
            e.ToView("VisitedStates");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<PlacePhoto>(e =>
        {
            e.HasIndex(p => p.PlaceId);
            e.HasIndex(p => p.UserId);
            e.Property(p => p.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<Place>().WithMany().HasForeignKey(p => p.PlaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlaceComment>(e =>
        {
            e.HasIndex(c => c.PlaceId);
            e.HasIndex(c => c.UserId);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<Place>().WithMany().HasForeignKey(c => c.PlaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PhotoRating>(e =>
        {
            e.HasKey(r => new { r.UserId, r.PhotoId });
            e.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<PlacePhoto>().WithMany().HasForeignKey(r => r.PhotoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommentRating>(e =>
        {
            e.HasKey(r => new { r.UserId, r.CommentId });
            e.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<PlaceComment>().WithMany().HasForeignKey(r => r.CommentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PointEvent>(e =>
        {
            e.HasIndex(p => p.UserId);
            e.HasIndex(p => p.EventType);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShareLink>(e =>
        {
            e.HasIndex(l => l.Token).IsUnique();
            e.HasIndex(l => l.UserId);
            e.Property(l => l.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCountry>(e =>
        {
            e.HasKey(uc => new { uc.UserId, uc.CountryId });
            e.HasIndex(uc => uc.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(uc => uc.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Country>().WithMany().HasForeignKey(uc => uc.CountryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
