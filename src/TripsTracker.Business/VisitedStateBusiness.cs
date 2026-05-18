using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class VisitedStateBusiness : BusinessBase<VisitedState>, IVisitedStateBusiness
{
    private readonly IUserContext _userContext;

    public VisitedStateBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public Task<List<VisitedStateDto>> GetAllAsync(CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(vs => vs.UserId == _userContext.UserId)
            .Select(vs => new VisitedStateDto(vs.Id, vs.CountryId, vs.StateAbbr, vs.StateName))
            .ToListAsync(ct);
}
