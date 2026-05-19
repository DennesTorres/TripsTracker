namespace TripsTracker.Interfaces.Integration;

public interface IGeoBoundariesService
{
    /// <summary>
    /// Fetches ADM1 (state/province) boundary GeoJSON for a country via the geoBoundaries API.
    /// </summary>
    /// <param name="isoAlpha3">ISO 3166-1 alpha-3 country code (e.g. "DEU").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Raw GeoJSON string, or null if the country is not found.</returns>
    Task<string?> GetBordersAsync(string isoAlpha3, CancellationToken ct = default);
}
