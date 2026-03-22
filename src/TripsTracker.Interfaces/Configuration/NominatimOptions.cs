using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Interfaces.Configuration;

public class NominatimOptions
{
    public const string SectionName = "Nominatim";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    [Required]
    public string UserAgent { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
