using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface ICountryBusiness
{
    Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default);
}
