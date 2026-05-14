using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TripsTracker.Data;

/// <summary>
/// Design-time factory used by EF Core tools (migrations, scaffolding).
/// Reads the local SQL Server connection string directly — not used at runtime.
/// </summary>
public class TripsTrackerDbContextFactory : IDesignTimeDbContextFactory<TripsTrackerDbContext>
{
    public TripsTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
            .UseSqlServer("Server=localhost;Database=TripsTracker_Stage5;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new TripsTrackerDbContext(options);
    }
}
