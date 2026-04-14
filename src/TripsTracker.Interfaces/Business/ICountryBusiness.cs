using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface ICountryBusiness
{
    Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CountryDto?> GetByIsoAlpha2Async(string isoAlpha2, CancellationToken ct = default);
    Task<CountryDto?> SetVisitedAsync(int id, bool isVisited, CancellationToken ct = default);
    Task<CountryDto?> SetHomeAsync(int id, bool isHome = true, CancellationToken ct = default);
    Task<CountryDto?> SetShowStateBordersAsync(int id, bool show, CancellationToken ct = default);
}
