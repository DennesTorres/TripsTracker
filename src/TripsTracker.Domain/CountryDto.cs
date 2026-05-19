namespace TripsTracker.Domain;

public class CountryDto
{
    public int Id { get; init; }
    public int IsoNumeric { get; init; }
    public required string IsoAlpha2 { get; init; }
    public required string Flag { get; init; }
    public required string Name { get; init; }
    public required string Region { get; init; }
    public bool IsHome { get; init; }
    public bool IsVisited { get; init; }
    public bool ShowStateBorders { get; init; }
}
