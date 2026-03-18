namespace TripsTracker.Data.Entities;

public class Country
{
    public int Id { get; set; }
    public int IsoNumeric { get; set; }
    public string Flag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public bool IsVisited { get; set; }
    public bool IsDeleted { get; set; }
}
