namespace TripsTracker.Data.Entities;

/// <summary>
/// Per-user country flags — replaces the global Countries.IsHome and Countries.IsVisited columns.
/// Composite PK: (UserId, CountryId).
/// </summary>
public class UserCountry
{
    public int UserId { get; set; }
    public int CountryId { get; set; }
    public bool IsHome { get; set; }
    public bool IsVisited { get; set; }
}
