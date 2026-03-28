namespace TripsTracker.Domain;

public record CitySuggestion(
    string City,
    string CountryName,
    string CountryIsoAlpha2,
    string? StateName,
    string? StateAbbr,
    double? Lat = null,
    double? Lon = null);
