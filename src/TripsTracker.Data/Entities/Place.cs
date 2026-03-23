using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Data.Entities;

public class Place
{
    public int Id { get; set; }
    public double Lon { get; set; }
    public double Lat { get; set; }
    public int CountryId { get; set; }
    public string City { get; set; } = string.Empty;
    [MaxLength(10)] public string? StateAbbr { get; set; }
    public string? StateName { get; set; }
    public bool IsHome { get; set; }
}
