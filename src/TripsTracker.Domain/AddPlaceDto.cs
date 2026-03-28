namespace TripsTracker.Domain;

public record AddPlaceDto(
    string CityName,
    string CountryIsoAlpha2,
    bool IsHome = false,
    double? Lat = null,
    double? Lon = null,
    string? StateAbbr = null,
    string? StateName = null);
