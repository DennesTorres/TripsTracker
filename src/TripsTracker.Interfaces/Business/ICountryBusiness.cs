using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface ICountryBusiness
{
    Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CountryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> GetByIsoAlpha2Async(string isoAlpha2, CancellationToken ct = default);
    Task<CountryDto?> SetVisitedAsync(int id, bool isVisited, CancellationToken ct = default);
    Task<CountryDto?> SetHomeAsync(int id, bool isHome = true, CancellationToken ct = default);
    /// <summary>
    /// Updates UserCountry.IsHome flags using bulk ExecuteUpdateAsync only — no SaveChangesAsync.
    /// Safe to call from PlacesProcess.SetHomeAsync where a Place entity is already tracked.
    /// Clears IsHome on all countries for this user, then sets it on the specified country.
    /// Requires the UserCountry row to already exist (call SetVisitedAsync first).
    /// </summary>
    Task SyncHomeFlagAsync(int countryId, CancellationToken ct = default);
    Task<CountryDto?> SetShowStateBordersAsync(int id, bool show, CancellationToken ct = default);
    Task<string?> GetIsoAlpha3Async(int countryId, CancellationToken ct = default);
}
