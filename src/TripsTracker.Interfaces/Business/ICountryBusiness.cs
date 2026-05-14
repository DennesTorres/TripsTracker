using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface ICountryBusiness
{
    Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CountryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> GetByIsoAlpha2Async(string isoAlpha2, CancellationToken ct = default);
    Task<CountryDto?> SetAsVisitedAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> UnsetVisitedAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> SetAsHomeAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> UnsetHomeAsync(int id, CancellationToken ct = default);
    Task<CountryDto?> SetShowStateBordersAsync(int id, bool show, CancellationToken ct = default);
    Task<List<CountryDto>> GetAllForUserAsync(int userId, CancellationToken ct = default);
}
