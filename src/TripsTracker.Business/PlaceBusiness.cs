using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class PlaceBusiness : BusinessBase<Place>, IPlaceBusiness
{
    public PlaceBusiness(TripsTrackerDbContext context) : base(context) { }

    public Task<List<PlaceDto>> GetAllAsync(CancellationToken ct = default)
        => BuildBaseQuery().Select(p => new PlaceDto(
            p.Id, p.Lon, p.Lat, p.Flag, p.CountryName, p.City, p.IsHome)).ToListAsync(ct);

    public Task<PlaceDto?> GetByIdAsync(int id, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.Id == id)
            .Select(p => new PlaceDto(p.Id, p.Lon, p.Lat, p.Flag, p.CountryName, p.City, p.IsHome))
            .FirstOrDefaultAsync(ct);

    public async Task<PlaceDto> CreateAsync(SavePlaceDto dto, CancellationToken ct = default)
    {
        var entity = new Place
        {
            Lon = dto.Lon,
            Lat = dto.Lat,
            Flag = dto.Flag,
            CountryName = dto.CountryName,
            City = dto.City,
            IsHome = dto.IsHome
        };
        await InsertAsync(entity, ct);
        return new PlaceDto(entity.Id, entity.Lon, entity.Lat, entity.Flag, entity.CountryName, entity.City, entity.IsHome);
    }

    public async Task<PlaceDto?> UpdateAsync(int id, SavePlaceDto dto, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            p => p.Id == id && !p.IsDeleted,
            s =>
            {
                s.SetProperty(p => p.Lon, dto.Lon);
                s.SetProperty(p => p.Lat, dto.Lat);
                s.SetProperty(p => p.Flag, dto.Flag);
                s.SetProperty(p => p.CountryName, dto.CountryName);
                s.SetProperty(p => p.City, dto.City);
                s.SetProperty(p => p.IsHome, dto.IsHome);
            },
            ct);
        if (rows == 0) return null;
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var rows = await ExecuteUpdateAsync(
            p => p.Id == id && !p.IsDeleted,
            s => s.SetProperty(p => p.IsDeleted, true),
            ct);
        return rows > 0;
    }
}
