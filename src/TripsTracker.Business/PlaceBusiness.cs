using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class PlaceBusiness : BusinessBase<Place>, IPlaceBusiness
{
    private readonly IUserContext _userContext;

    public PlaceBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public async Task<PlaceDto> CreateAsync(CreatePlaceDto dto, CancellationToken ct = default)
    {
        // Find or create the global Place by (City, CountryId) — PLACE_IMMUTABILITY: coordinates set once
        var existingPlace = await BuildBaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.City == dto.City && p.CountryId == dto.CountryId, ct);

        int placeId;
        if (existingPlace == null)
        {
            var place = new Place
            {
                Lon = dto.Lon,
                Lat = dto.Lat,
                CountryId = dto.CountryId,
                City = dto.City,
                StateAbbr = dto.StateAbbr,
                StateName = dto.StateName,
            };
            await InsertAsync(place, ct);
            placeId = place.Id;
        }
        else
        {
            placeId = existingPlace.Id;
        }

        // VISITED_MEANS_LINKED: create UserPlaces row — IsHome always false here; set via SetHomeAsync
        var userPlace = new UserPlace
        {
            UserId = _userContext.UserId!.Value,
            PlaceId = placeId,
            IsHome = false,
        };
        Context.Set<UserPlace>().Add(userPlace);
        await Context.SaveChangesAsync(ct);

        return await GetByIdAsync(placeId, ct)
            ?? throw new InvalidOperationException($"Failed to retrieve created place {placeId}.");
    }

    public Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default)
        => BuildBaseQuery()
            .Join(Context.Set<UserPlace>().AsNoTracking(),
                p => p.Id,
                up => up.PlaceId,
                (p, up) => new { p, up })
            .Where(x => x.up.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(),
                x => x.p.CountryId,
                c => c.Id,
                (x, c) => new PlaceDto(x.p.Id, x.p.Lon, x.p.Lat, x.p.CountryId, c.Name, c.Flag, x.p.City, x.p.StateAbbr, x.p.StateName, x.up.IsHome))
            .ToListAsync(ct);

    public Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.Id == id)
            .Join(Context.Set<UserPlace>().AsNoTracking(),
                p => p.Id,
                up => up.PlaceId,
                (p, up) => new { p, up })
            .Where(x => x.up.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(),
                x => x.p.CountryId,
                c => c.Id,
                (x, c) => new PlaceDto(x.p.Id, x.p.Lon, x.p.Lat, x.p.CountryId, c.Name, c.Flag, x.p.City, x.p.StateAbbr, x.p.StateName, x.up.IsHome))
            .FirstOrDefaultAsync(ct);

    public async Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default)
    {
        // HOME_EXCLUSIVITY: only one place per user may be IsHome at a time
        if (dto.IsHome)
        {
            await Context.Set<UserPlace>()
                .Where(up => up.UserId == _userContext.UserId && up.PlaceId != id && up.IsHome)
                .ExecuteUpdateAsync(s => s.SetProperty(up => up.IsHome, false), ct);
        }

        var rows = await Context.Set<UserPlace>()
            .Where(up => up.PlaceId == id && up.UserId == _userContext.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(up => up.IsHome, dto.IsHome), ct);
        if (rows == 0) return null;
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        // DELETE_IS_UNLINK: remove the UserPlaces row; global Place row stays
        var rows = await Context.Set<UserPlace>()
            .Where(up => up.PlaceId == id && up.UserId == _userContext.UserId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public Task<bool> HasAnyInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.CountryId == countryId)
            .Join(Context.Set<UserPlace>().AsNoTracking(),
                p => p.Id,
                up => up.PlaceId,
                (p, up) => up)
            .AnyAsync(up => up.UserId == _userContext.UserId, ct);

    public Task<bool> HasHomeInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.CountryId == countryId)
            .Join(Context.Set<UserPlace>().AsNoTracking(),
                p => p.Id,
                up => up.PlaceId,
                (p, up) => up)
            .AnyAsync(up => up.UserId == _userContext.UserId && up.IsHome, ct);

    public Task ClearAllHomePlacesAsync(CancellationToken ct = default)
        => Context.Set<UserPlace>()
            .Where(up => up.UserId == _userContext.UserId && up.IsHome)
            .ExecuteUpdateAsync(s => s.SetProperty(up => up.IsHome, false), ct);

    public Task MarkAsHomeAsync(int placeId, CancellationToken ct = default)
        => Context.Set<UserPlace>()
            .Where(up => up.PlaceId == placeId && up.UserId == _userContext.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(up => up.IsHome, true), ct);

    public Task<CreatePlaceDto?> FindGlobalAsync(string city, int countryId, CancellationToken ct = default)
        => BuildBaseQuery()
            .AsNoTracking()
            .Where(p => p.City == city && p.CountryId == countryId)
            .Select(p => new CreatePlaceDto(p.Lon, p.Lat, p.CountryId, p.City, p.StateAbbr, p.StateName))
            .FirstOrDefaultAsync(ct);

    public Task<List<VisitedStateDto>> GetVisitedStatesAsync(CancellationToken ct = default)
        => Context.Set<VisitedState>().AsNoTracking()
            .Where(vs => vs.UserId == _userContext.UserId)
            .Select(vs => new VisitedStateDto(vs.Id, vs.CountryId, vs.StateAbbr, vs.StateName))
            .ToListAsync(ct);
}
