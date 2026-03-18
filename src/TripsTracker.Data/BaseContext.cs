using Microsoft.EntityFrameworkCore;

namespace TripsTracker.Data;

/// <summary>
/// Injectable base class for all application DbContexts.
/// Inherit from this and pass <typeparamref name="TContext"/> to DbContextOptions
/// so EF Core's DI integration works correctly.
/// </summary>
public abstract class BaseContext<TContext> : DbContext
    where TContext : DbContext
{
    protected BaseContext(DbContextOptions<TContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Prefer DateTimeOffset over DateTime for all entities
        foreach (var property in modelBuilder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("datetime2");
        }
    }
}
