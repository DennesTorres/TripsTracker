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
            vs.Id, vs.CountryCode, vs.StateAbbr)).ToListAsync(ct);

    public async Task<VisitedStateDto> SetVisitedAsync(string countryCode, string stateAbbr, CancellationToken ct = default)
    {
        // Restore soft-deleted or insert new
        var existing = await Context.Set<VisitedState>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(vs => vs.CountryCode == countryCode && vs.StateAbbr == stateAbbr, ct);

        if (existing is not null)
        {
            existing.IsDeleted = false;
            await Context.SaveChangesAsync(ct);
            return new VisitedStateDto(existing.Id, existing.CountryCode, existing.StateAbbr);
        }

        var entity = new VisitedState { CountryCode = countryCode, StateAbbr = stateAbbr };
        await InsertAsync(entity, ct);
        return new VisitedStateDto(entity.Id, entity.CountryCode, entity.StateAbbr);
    }

    public async Task<bool> ClearVisitedAsync(string countryCode, string stateAbbr, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            vs => vs.CountryCode == countryCode && vs.StateAbbr == stateAbbr && !vs.IsDeleted,
            s => s.SetProperty(vs => vs.IsDeleted, true),
            ct);
        return rows > 0;
    }
}
