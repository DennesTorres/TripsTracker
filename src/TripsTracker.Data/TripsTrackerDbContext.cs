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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Place>(e =>
        {
            e.HasIndex(p => p.CountryId);
            e.HasIndex(p => p.UserId);
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

        modelBuilder.Entity<UserCountry>(e =>
        {
            e.HasKey(uc => new { uc.UserId, uc.CountryId });
            e.HasIndex(uc => uc.UserId);
        });
    }
}
