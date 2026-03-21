namespace TripsTracker.Data.Entities;

public class Place
{
    public int Id { get; set; }
    public double Lon { get; set; }
    public double Lat { get; set; }
    public int CountryId { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateAbbr { get; set; }
    public bool IsHome { get; set; }
    public bool IsDeleted { get; set; }
}
