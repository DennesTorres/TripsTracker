using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class CountryBusiness : BusinessBase<Country, CountryDto>, ICountryBusiness
{
    public CountryBusiness(TripsTrackerDbContext context) : base(context) { }

    public Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default)
        => ToListAsync(BuildBaseQuery().Select(c => new CountryDto(
            c.Id, c.IsoNumeric, c.IsoAlpha2, c.Flag, c.Name, c.Region, c.IsHome, c.IsVisited)), ct);

    public async Task<CountryDto?> SetVisitedAsync(int id, bool isVisited, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            c => c.Id == id && !c.IsDeleted,
            s => s.SetProperty(c => c.IsVisited, isVisited),
            ct);
        if (rows == 0) return null;
        return await GetByIdAsync(
            c => c.Id == id,
            c => new CountryDto(c.Id, c.IsoNumeric, c.IsoAlpha2, c.Flag, c.Name, c.Region, c.IsHome, c.IsVisited),
            ct);
    }

    public async Task<CountryDto?> SetHomeAsync(int id, CancellationToken ct = default)
    {
        // Clear existing home flag, then set on requested country
        await Context.Set<Country>()
            .Where(c => c.IsHome && !c.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsHome, false), ct);

        var rows = await ExecuteUpdateAsync(
            c => c.Id == id && !c.IsDeleted,
            s =>
            {
                s.SetProperty(c => c.IsHome, true);
                s.SetProperty(c => c.IsVisited, true);
            },
            ct);
        if (rows == 0) return null;
        return await GetByIdAsync(
            c => c.Id == id,
            c => new CountryDto(c.Id, c.IsoNumeric, c.IsoAlpha2, c.Flag, c.Name, c.Region, c.IsHome, c.IsVisited),
            ct);
    }
}
