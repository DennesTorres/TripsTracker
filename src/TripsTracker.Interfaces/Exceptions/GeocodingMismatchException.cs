namespace TripsTracker.Interfaces.Exceptions;

/// <summary>
/// Thrown when Nominatim finds a result but its canonical city name does not match the user input.
/// Carries the canonical name so the caller can suggest it to the user.
/// Maps to HTTP 422 Unprocessable Entity (via <see cref="BusinessRuleException"/> hierarchy).
/// </summary>
public class GeocodingMismatchException : BusinessRuleException
{
    public string SuggestedCity { get; }

    public GeocodingMismatchException(string userInput, string suggestedCity, string countryName)
        : base(
            $"'{userInput}' was not found in {countryName}, but '{suggestedCity}' was. Did you mean '{suggestedCity}'?",
            "GEOCODING_MISMATCH")
    {
        SuggestedCity = suggestedCity;
    }
}
