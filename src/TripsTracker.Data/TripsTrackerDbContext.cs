using Microsoft.EntityFrameworkCore;
using TripsTracker.Data.Entities;

namespace TripsTracker.Data;

public class TripsTrackerDbContext : BaseContext<TripsTrackerDbContext>
{
    public TripsTrackerDbContext(DbContextOptions<TripsTrackerDbContext> options) : base(options) { }

    public DbSet<Place> Places => Set<Place>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<VisitedState> VisitedStates => Set<VisitedState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Place>(e =>
        {
            e.HasIndex(p => p.CountryId);
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<Country>(e =>
        {
            e.HasIndex(c => c.IsoNumeric).IsUnique();
            e.HasIndex(c => c.Name);
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<VisitedState>(e =>
        {
            e.ToView("VisitedStates");
        });
    }
}
