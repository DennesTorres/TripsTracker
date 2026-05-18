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
        var place = new Place
        {
            Lon = dto.Lon,
            Lat = dto.Lat,
            CountryId = dto.CountryId,
            City = dto.City,
            StateAbbr = dto.StateAbbr,
            StateName = dto.StateName,
            IsHome = dto.IsHome,
            UserId = _userContext.UserId
        };
        await InsertAsync(place, ct);
        return await GetByIdAsync(place.Id, ct)
            ?? throw new InvalidOperationException($"Failed to retrieve created place {place.Id}.");
    }

    public Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(),
                p => p.CountryId,
                c => c.Id,
                (p, c) => new PlaceDto(p.Id, p.Lon, p.Lat, p.CountryId, c.Name, c.Flag, p.City, p.StateAbbr, p.StateName, p.IsHome))
            .ToListAsync(ct);

    public Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.Id == id && p.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(),
                p => p.CountryId,
                c => c.Id,
                (p, c) => new PlaceDto(p.Id, p.Lon, p.Lat, p.CountryId, c.Name, c.Flag, p.City, p.StateAbbr, p.StateName, p.IsHome))
            .FirstOrDefaultAsync(ct);

    public async Task<PlaceDto?> UpdateAsync(int id, UpdatePlaceDto dto, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            p => p.Id == id && p.UserId == _userContext.UserId,
            s =>
            {
                s.SetProperty(p => p.City, dto.City);
                s.SetProperty(p => p.IsHome, dto.IsHome);
            },
            ct);
        if (rows == 0) return null;
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var rows = await ExecuteDeleteAsync(p => p.Id == id && p.UserId == _userContext.UserId, ct);
        return rows > 0;
    }

    public Task<bool> HasAnyInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery().AnyAsync(p => p.CountryId == countryId && p.UserId == _userContext.UserId, ct);

    public Task<bool> HasHomeInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery().AnyAsync(p => p.CountryId == countryId && p.IsHome && p.UserId == _userContext.UserId, ct);

}
