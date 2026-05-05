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
            UserId = _userContext.UserId!.Value
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

    public Task<List<PlaceDto>> GetAllForUserAsync(int userId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.UserId == userId)
            .Join(Context.Set<Country>().AsNoTracking(),
                p => p.CountryId,
                c => c.Id,
                (p, c) => new PlaceDto(p.Id, p.Lon, p.Lat, p.CountryId, c.Name, c.Flag, p.City, p.StateAbbr, p.StateName, p.IsHome))
            .ToListAsync(ct);

    public Task<bool> HasAnyForCurrentUserInRegionAsync(string region, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(), p => p.CountryId, c => c.Id, (p, c) => c.Region)
            .AnyAsync(r => r == region, ct);

    public Task<bool> HasAnyGloballyInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery().AnyAsync(p => p.CountryId == countryId, ct);

    public Task<bool> HasAnyGloballyInRegionAsync(string region, CancellationToken ct = default)
        => BuildBaseQuery()
            .Join(Context.Set<Country>().AsNoTracking(), p => p.CountryId, c => c.Id, (p, c) => c.Region)
            .AnyAsync(r => r == region, ct);

    public Task<PlaceDto?> GetFirstForCurrentUserInCountryAsync(int countryId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.CountryId == countryId && p.UserId == _userContext.UserId)
            .OrderBy(p => p.Id)
            .Join(Context.Set<Country>().AsNoTracking(),
                p => p.CountryId,
                c => c.Id,
                (p, c) => new PlaceDto(p.Id, p.Lon, p.Lat, p.CountryId, c.Name, c.Flag, p.City, p.StateAbbr, p.StateName, p.IsHome))
            .FirstOrDefaultAsync(ct);

    public Task<PlaceDto?> GetFirstForCurrentUserInRegionAsync(string region, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.UserId == _userContext.UserId)
            .Join(Context.Set<Country>().AsNoTracking(), p => p.CountryId, c => c.Id, (p, c) => new { Place = p, Country = c })
            .Where(x => x.Country.Region == region)
            .OrderBy(x => x.Place.Id)
            .Select(x => new PlaceDto(x.Place.Id, x.Place.Lon, x.Place.Lat, x.Place.CountryId, x.Country.Name, x.Country.Flag, x.Place.City, x.Place.StateAbbr, x.Place.StateName, x.Place.IsHome))
            .FirstOrDefaultAsync(ct);
}
