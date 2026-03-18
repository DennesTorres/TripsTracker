namespace TripsTracker.Data.Entities;

public class Place
{
    public int Id { get; set; }
    public double Lon { get; set; }
    public double Lat { get; set; }
    public string Flag { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public bool IsDeleted { get; set; }
}
