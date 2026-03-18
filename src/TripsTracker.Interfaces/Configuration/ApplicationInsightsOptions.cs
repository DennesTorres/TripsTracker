using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Interfaces.Configuration;

/// <summary>
/// Configuration options for Application Insights.
/// Bound from the "ApplicationInsights" section in application settings.
/// </summary>
public class ApplicationInsightsOptions
{
    public const string SectionName = "ApplicationInsights";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
