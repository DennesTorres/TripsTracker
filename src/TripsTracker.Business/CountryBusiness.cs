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
            c.Id, c.IsoNumeric, c.Flag, c.Name, c.Region, c.IsHome, c.IsVisited)), ct);
}
