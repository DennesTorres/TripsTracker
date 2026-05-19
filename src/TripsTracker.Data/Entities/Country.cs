namespace TripsTracker.Data.Entities;

public class Country
{
    public int Id { get; set; }
    public int IsoNumeric { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? IsoAlpha3 { get; set; }
}
