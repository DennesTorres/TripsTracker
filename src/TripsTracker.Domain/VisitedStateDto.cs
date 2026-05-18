namespace TripsTracker.Domain;

public record VisitedStateDto(int Id, int CountryId, string StateAbbr, string? StateName);
