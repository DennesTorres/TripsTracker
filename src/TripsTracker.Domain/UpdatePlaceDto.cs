namespace TripsTracker.Domain;

/// <summary>
/// User-editable fields for an existing place.
/// Lat/Lon, CountryId, StateAbbr, and CountryFlag are all geocoding-derived and cannot be updated directly.
/// </summary>
public record UpdatePlaceDto(string City, bool IsHome);
