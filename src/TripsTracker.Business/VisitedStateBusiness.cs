using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class VisitedStateBusiness : BusinessBase<VisitedState>, IVisitedStateBusiness
{
    public VisitedStateBusiness(TripsTrackerDbContext context) : base(context) { }

    public Task<List<VisitedStateDto>> GetAllAsync(CancellationToken ct = default)
        => BuildBaseQuery().Select(vs => new VisitedStateDto(
            vs.Id, vs.CountryId, vs.StateAbbr, vs.StateName)).ToListAsync(ct);
}
