using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IDeletePlaceProcess
{
    /// <summary>
    /// Deletes a place and manages related country flags:
    /// - Unsets country IsVisited if this was the last place in the country.
    /// - Returns PromptHomeCountry=true if the deleted place was the user's home city,
    ///   so the frontend can ask whether the country is still the home country.
    /// </summary>
    Task<DeletePlaceResult> ExecuteAsync(int placeId, CancellationToken ct = default);
}
