namespace TripsTracker.Domain;

public record CountryDto(int Id, int IsoNumeric, string Flag, string Name, string Region, bool IsHome, bool IsVisited);
