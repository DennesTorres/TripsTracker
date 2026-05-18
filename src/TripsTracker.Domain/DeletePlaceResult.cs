namespace TripsTracker.Domain;

/// <summary>
/// Result of a place deletion. When PromptHomeCountry is true, the deleted place was
/// the user's home city and the frontend must ask whether the country is still home.
/// </summary>
public record DeletePlaceResult(bool PromptHomeCountry, int? CountryId, string? CountryName);
