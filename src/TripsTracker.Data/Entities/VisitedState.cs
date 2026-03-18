namespace TripsTracker.Data.Entities;

public class VisitedState
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string StateAbbr { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
