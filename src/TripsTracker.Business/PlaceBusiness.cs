using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class PlaceBusiness : BusinessBase<Place, PlaceDto>, IPlaceBusiness
{
    public PlaceBusiness(TripsTrackerDbContext context) : base(context) { }

    public Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default)
        => ToListAsync(BuildBaseQuery().Select(p => new PlaceDto(
            p.Id, p.Lon, p.Lat, p.Flag, p.CountryName, p.City, p.IsHome)), ct);
}
