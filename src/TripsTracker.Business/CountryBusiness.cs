using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class CountryBusiness : BusinessBase<Country>, ICountryBusiness
{
    private readonly IUserContext _userContext;

    public CountryBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public Task<List<CountryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var userId = _userContext.UserId;
        return BuildBaseQuery()
            .GroupJoin(
                Context.Set<UserCountry>().AsNoTracking().Where(uc => uc.UserId == userId),
                c => c.Id,
                uc => uc.CountryId,
                (c, ucs) => new { c, ucs })
            .SelectMany(
                x => x.ucs.DefaultIfEmpty(),
                (x, uc) => new CountryDto(
                    x.c.Id, x.c.IsoNumeric, x.c.IsoAlpha2, x.c.Flag, x.c.Name, x.c.Region,
                    uc != null && uc.IsHome,
                    uc != null && uc.IsVisited,
                    uc != null && uc.ShowStateBorders))
            .ToListAsync(ct);
    }

    public Task<CountryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var userId = _userContext.UserId;
        return BuildBaseQuery()
            .Where(c => c.Id == id)
            .GroupJoin(
                Context.Set<UserCountry>().AsNoTracking().Where(uc => uc.UserId == userId),
                c => c.Id,
                uc => uc.CountryId,
                (c, ucs) => new { c, ucs })
            .SelectMany(
                x => x.ucs.DefaultIfEmpty(),
                (x, uc) => new CountryDto(
                    x.c.Id, x.c.IsoNumeric, x.c.IsoAlpha2, x.c.Flag, x.c.Name, x.c.Region,
                    uc != null && uc.IsHome,
                    uc != null && uc.IsVisited,
                    uc != null && uc.ShowStateBorders))
            .FirstOrDefaultAsync(ct);
    }

    public Task<CountryDto?> GetByIsoAlpha2Async(string isoAlpha2, CancellationToken ct = default)
    {
        var userId = _userContext.UserId;
        return BuildBaseQuery()
            .Where(c => c.IsoAlpha2 == isoAlpha2)
            .GroupJoin(
                Context.Set<UserCountry>().AsNoTracking().Where(uc => uc.UserId == userId),
                c => c.Id,
                uc => uc.CountryId,
                (c, ucs) => new { c, ucs })
            .SelectMany(
                x => x.ucs.DefaultIfEmpty(),
                (x, uc) => new CountryDto(
                    x.c.Id, x.c.IsoNumeric, x.c.IsoAlpha2, x.c.Flag, x.c.Name, x.c.Region,
                    uc != null && uc.IsHome,
                    uc != null && uc.IsVisited,
                    uc != null && uc.ShowStateBorders))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CountryDto?> SetVisitedAsync(int id, bool isVisited, CancellationToken ct = default)
    {
        var userId = _userContext.UserId ?? throw new InvalidOperationException("Not authenticated.");
        await UpsertUserCountryAsync(userId, id, isVisited: isVisited, ct: ct);
        return await GetByIdForUserAsync(id, userId, ct);
    }

    public async Task<CountryDto?> SetHomeAsync(int id, bool isHome = true, CancellationToken ct = default)
    {
        var userId = _userContext.UserId ?? throw new InvalidOperationException("Not authenticated.");

        if (isHome)
        {
            // Clear existing home flag for this user across all countries
            await Context.Set<UserCountry>()
                .Where(uc => uc.UserId == userId && uc.IsHome)
                .ExecuteUpdateAsync(s => s.SetProperty(uc => uc.IsHome, false), ct);

            // Set home + visited on the requested country
            await UpsertUserCountryAsync(userId, id, isHome: true, isVisited: true, ct: ct);
        }
        else
        {
            await UpsertUserCountryAsync(userId, id, isHome: false, ct: ct);
        }

        return await GetByIdForUserAsync(id, userId, ct);
    }

    public async Task<CountryDto?> SetShowStateBordersAsync(int id, bool show, CancellationToken ct = default)
    {
        var userId = _userContext.UserId ?? throw new InvalidOperationException("Not authenticated.");
        await UpsertUserCountryAsync(userId, id, showStateBorders: show, ct: ct);
        return await GetByIdForUserAsync(id, userId, ct);
    }

    public Task<List<CountryDto>> GetAllForUserAsync(int userId, CancellationToken ct = default)
        => BuildBaseQuery()
            .GroupJoin(
                Context.Set<UserCountry>().AsNoTracking().Where(uc => uc.UserId == userId),
                c => c.Id,
                uc => uc.CountryId,
                (c, ucs) => new { c, ucs })
            .SelectMany(
                x => x.ucs.DefaultIfEmpty(),
                (x, uc) => new CountryDto(
                    x.c.Id, x.c.IsoNumeric, x.c.IsoAlpha2, x.c.Flag, x.c.Name, x.c.Region,
                    uc != null && uc.IsHome,
                    uc != null && uc.IsVisited,
                    uc != null && uc.ShowStateBorders))
            .ToListAsync(ct);

    // ── private helpers ──────────────────────────────────────────────────────────

    private async Task UpsertUserCountryAsync(
        int userId, int countryId,
        bool? isHome = null, bool? isVisited = null, bool? showStateBorders = null,
        CancellationToken ct = default)
    {
        var existing = await Context.Set<UserCountry>()
            .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CountryId == countryId, ct);

        if (existing is null)
        {
            var newUc = new UserCountry
            {
                UserId = userId,
                CountryId = countryId,
                IsHome = isHome ?? false,
                IsVisited = isVisited ?? false,
                ShowStateBorders = showStateBorders ?? false
            };
            Context.Set<UserCountry>().Add(newUc);
            await Context.SaveChangesAsync(ct);
        }
        else
        {
            if (isHome.HasValue) existing.IsHome = isHome.Value;
            if (isVisited.HasValue) existing.IsVisited = isVisited.Value;
            if (showStateBorders.HasValue) existing.ShowStateBorders = showStateBorders.Value;
            await Context.SaveChangesAsync(ct);
        }
    }

    private Task<CountryDto?> GetByIdForUserAsync(int id, int userId, CancellationToken ct)
        => BuildBaseQuery()
            .Where(c => c.Id == id)
            .GroupJoin(
                Context.Set<UserCountry>().AsNoTracking().Where(uc => uc.UserId == userId),
                c => c.Id,
                uc => uc.CountryId,
                (c, ucs) => new { c, ucs })
            .SelectMany(
                x => x.ucs.DefaultIfEmpty(),
                (x, uc) => new CountryDto(
                    x.c.Id, x.c.IsoNumeric, x.c.IsoAlpha2, x.c.Flag, x.c.Name, x.c.Region,
                    uc != null && uc.IsHome,
                    uc != null && uc.IsVisited,
                    uc != null && uc.ShowStateBorders))
            .FirstOrDefaultAsync(ct);
}
