using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Interfaces.Configuration;

/// <summary>
/// Configuration options for the database connection.
/// Bound from the "Database" section in application settings.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
