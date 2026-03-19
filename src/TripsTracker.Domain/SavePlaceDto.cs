namespace TripsTracker.Domain;

public record SavePlaceDto(double Lon, double Lat, string Flag, string CountryName, string City, bool IsHome);
