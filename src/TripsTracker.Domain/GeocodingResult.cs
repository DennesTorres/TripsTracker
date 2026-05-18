namespace TripsTracker.Domain;

public record GeocodingResult(
    double Lat,
    double Lon,
    string City,
    string? StateAbbr,
    string? StateName,
    string CountryIsoAlpha2);
