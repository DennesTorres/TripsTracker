namespace TripsTracker.Domain;

public record CreatePlaceDto(double Lon, double Lat, int CountryId, string City, string? StateAbbr, bool IsHome);
