using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IVisitedStateBusiness
{
    Task<List<VisitedStateDto>> GetAllAsync(CancellationToken ct = default);
    Task<VisitedStateDto> SetVisitedAsync(string countryCode, string stateAbbr, CancellationToken ct = default);
    Task<bool> ClearVisitedAsync(string countryCode, string stateAbbr, CancellationToken ct = default);
}
