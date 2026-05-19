namespace TripsTracker.Domain;

/// <summary>
/// User-editable fields for an existing place.
/// City, Lat/Lon, CountryId, StateAbbr, and CountryFlag are all geocoding-derived and cannot be updated directly.
/// The only user-editable attribute is IsHome.
/// </summary>
public record UpdatePlaceDto(bool IsHome);
