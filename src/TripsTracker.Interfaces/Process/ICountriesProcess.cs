namespace TripsTracker.Interfaces.Process;

public interface ICountriesProcess
{
    /// <summary>
    /// Returns the ADM1 GeoJSON borders for a country, or null if unavailable.
    /// </summary>
    Task<string?> GetBordersAsync(int countryId, CancellationToken ct = default);
}
