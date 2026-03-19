using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface ICountryBusiness
{
    Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CountryDto?> SetVisitedAsync(int id, bool isVisited, CancellationToken ct = default);
    Task<CountryDto?> SetHomeAsync(int id, CancellationToken ct = default);
}
