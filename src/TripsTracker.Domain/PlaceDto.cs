namespace TripsTracker.Domain;

public record PlaceDto(int Id, double Lon, double Lat, string Flag, string CountryName, string City, bool IsHome);
