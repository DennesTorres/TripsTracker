namespace TripsTracker.Domain;

/// <summary>
/// User-editable fields for an existing place.
/// City, Lat/Lon, CountryId, StateAbbr, and CountryFlag are all geocoding-derived and immutable (PLACE_IMMUTABILITY).
/// </summary>
public record UpdatePlaceDto(bool IsHome);
