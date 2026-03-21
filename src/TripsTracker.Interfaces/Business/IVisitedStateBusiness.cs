using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IVisitedStateBusiness
{
    Task<List<VisitedStateDto>> GetAllAsync(CancellationToken ct = default);
}
