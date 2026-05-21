namespace TripsTracker.Domain;

/// <summary>
/// User-editable fields for an existing place.
/// City, Lat/Lon, CountryId, StateAbbr, and CountryFlag are all geocoding-derived and cannot be updated directly.
/// </summary>
public record UpdatePlaceDto(bool IsHome);
