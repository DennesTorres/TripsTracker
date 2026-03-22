namespace TripsTracker.Domain;

public record PlaceDto(int Id, double Lon, double Lat, int CountryId, string CountryName, string CountryFlag, string City, string? StateAbbr, bool IsHome);
