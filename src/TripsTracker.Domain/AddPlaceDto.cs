namespace TripsTracker.Domain;

public record AddPlaceDto(string CityName, string CountryIsoAlpha2, bool IsHome = false);
