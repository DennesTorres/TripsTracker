using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;

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
        var userId = _userContext.UserId!.Value;
        var duplicate = await BuildBaseQuery()
            .AnyAsync(p => p.UserId == userId && p.CountryId == dto.CountryId
                && p.City.ToLower() == dto.City.ToLower(), ct);
        if (duplicate)
            throw new BusinessRuleException(
                $"You already have '{dto.City}' in your places.", "DUPLICATE_PLACE");

        if (dto.IsHome) await ClearAllHomePlacesAsync(ct);

        var place = new Place
        {
            Lon = dto.Lon,
            Lat = dto.Lat,
            CountryId = dto.CountryId,
            City = dto.City,
            StateAbbr = dto.StateAbbr,
            StateName = dto.StateName,
            IsHome = dto.IsHome,
            UserId = userId
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
        if (dto.IsHome) await ClearAllHomePlacesAsync(ct);
        var rows = await ExecuteUpdateAsync(
            p => p.Id == id && p.UserId == _userContext.UserId,
            s => s.SetProperty(p => p.IsHome, dto.IsHome),
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

    public Task ClearAllHomePlacesAsync(CancellationToken ct = default)
        => ExecuteUpdateAsync(
            p => p.UserId == _userContext.UserId && p.IsHome,
            s => s.SetProperty(p => p.IsHome, false),
            ct);
}
