namespace TripsTracker.Data.Entities;

public class VisitedState
{
    public int Id { get; set; }
    public int CountryId { get; set; }
    public string StateAbbr { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
