namespace TripsTracker.Domain;

public record CountryDto(int Id, int IsoNumeric, string IsoAlpha2, string? IsoAlpha3, string Flag, string Name, string Region, bool IsHome, bool IsVisited, bool ShowStateBorders);
