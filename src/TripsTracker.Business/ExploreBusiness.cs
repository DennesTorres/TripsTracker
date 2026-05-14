using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class ExploreBusiness : BusinessBase<Place>, IExploreBusiness
{
    public ExploreBusiness(TripsTrackerDbContext context) : base(context) { }

    public async Task<List<ExploreLocationDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var places = await BuildBaseQuery()
            .Where(p => string.IsNullOrEmpty(query) || EF.Functions.Like(p.City, $"%{query}%"))
            .Join(Context.Set<Country>().AsNoTracking(),
                p => p.CountryId, c => c.Id,
                (p, c) => new { p.Id, p.City, p.StateName, p.CountryId, CountryName = c.Name, p.Lat, p.Lon })
            .ToListAsync(ct);

        var photoCountsByPlace = await Context.Set<PlacePhoto>().AsNoTracking()
            .GroupBy(ph => ph.PlaceId)
            .Select(g => new { PlaceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var photoMap = photoCountsByPlace.ToDictionary(x => x.PlaceId, x => x.Count);

        var commentCountsByPlace = await Context.Set<PlaceComment>().AsNoTracking()
            .GroupBy(c => c.PlaceId)
            .Select(g => new { PlaceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var commentMap = commentCountsByPlace.ToDictionary(x => x.PlaceId, x => x.Count);

        return places
            .GroupBy(p => (p.City, p.CountryId))
            .Select(g => new ExploreLocationDto { City = g.Key.City, StateName = g.Select(x => x.StateName).FirstOrDefault(s => s != null), CountryName = g.First().CountryName, CountryId = g.Key.CountryId, Lat = g.Average(x => x.Lat), Lon = g.Average(x => x.Lon), UserCount = g.Count(), PhotoCount = g.Sum(x => photoMap.GetValueOrDefault(x.Id, 0)), CommentCount = g.Sum(x => commentMap.GetValueOrDefault(x.Id, 0)) })
            .OrderByDescending(l => l.UserCount)
            .Take(20)
            .ToList();
    }

    public async Task<ExploreContentDto> GetContentAsync(string city, int countryId, CancellationToken ct = default)
    {
        var placeIds = await BuildBaseQuery()
            .Where(p => p.City == city && p.CountryId == countryId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (placeIds.Count == 0)
            return new ExploreContentDto { Photos = [], Comments = [] };

        var photos = await Context.Set<PlacePhoto>().AsNoTracking()
            .Where(ph => placeIds.Contains(ph.PlaceId))
            .OrderBy(ph => ph.SortOrder)
            .GroupJoin(
                Context.Set<PhotoRating>().AsNoTracking(),
                ph => ph.Id,
                r => r.PhotoId,
                (ph, ratings) => new { ph, ratings })
            .Select(x => new PlacePhotoDto { Id = x.ph.Id, PlaceId = x.ph.PlaceId, UserId = x.ph.UserId, OriginalFileName = x.ph.OriginalFileName, ContentType = x.ph.ContentType, SizeBytes = x.ph.SizeBytes, Caption = x.ph.Caption, SortOrder = x.ph.SortOrder, UploadedAt = x.ph.UploadedAt, AverageRating = x.ratings.Any() ? x.ratings.Average(r => (double)r.Rating) : 0, RatingCount = x.ratings.Count() })
            .ToListAsync(ct);

        var comments = await Context.Set<PlaceComment>().AsNoTracking()
            .Where(c => placeIds.Contains(c.PlaceId))
            .OrderByDescending(c => c.CreatedAt)
            .Join(Context.Set<User>().AsNoTracking(),
                c => c.UserId, u => u.Id,
                (c, u) => new { c, DisplayName = u.DisplayName ?? u.Email })
            .GroupJoin(
                Context.Set<CommentRating>().AsNoTracking(),
                x => x.c.Id,
                r => r.CommentId,
                (x, ratings) => new { x.c, x.DisplayName, ratings })
            .Select(x => new PlaceCommentDto { Id = x.c.Id, PlaceId = x.c.PlaceId, UserId = x.c.UserId, UserDisplayName = x.DisplayName, Text = x.c.Text, CreatedAt = x.c.CreatedAt, UpdatedAt = x.c.UpdatedAt, UpvoteCount = x.ratings.Count(r => r.IsUpvote), DownvoteCount = x.ratings.Count(r => !r.IsUpvote), ParentCommentId = x.c.ParentCommentId })
            .ToListAsync(ct);

        return new ExploreContentDto { Photos = photos, Comments = comments };
    }
}
