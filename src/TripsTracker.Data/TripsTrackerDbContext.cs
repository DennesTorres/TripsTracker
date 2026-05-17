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
    public DbSet<UserPlace> UserPlaces => Set<UserPlace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Place>(e =>
        {
            e.HasIndex(p => p.CountryId);
            e.HasOne<Country>().WithMany().HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPlace>(e =>
        {
            e.HasKey(up => new { up.UserId, up.PlaceId });
            e.HasOne<User>().WithMany().HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Place>().WithMany().HasForeignKey(up => up.PlaceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Country>(e =>
        {
            e.HasIndex(c => c.IsoNumeric).IsUnique();
            e.HasIndex(c => c.Name);
            e.Property(c => c.IsoAlpha3).HasMaxLength(3);
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

        modelBuilder.Entity<UserCountry>(e =>
        {
            e.HasKey(uc => new { uc.UserId, uc.CountryId });
            e.HasIndex(uc => uc.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(uc => uc.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Country>().WithMany().HasForeignKey(uc => uc.CountryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
