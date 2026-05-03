using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;

namespace TripsTracker.Business;

public class PlacePhotoBusiness : BusinessBase<PlacePhoto>, IPlacePhotoBusiness
{
    private readonly IUserContext _userContext;

    public PlacePhotoBusiness(TripsTrackerDbContext context, IUserContext userContext) : base(context)
    {
        _userContext = userContext;
    }

    public async Task<PlacePhotoDto> CreateAsync(
        int placeId, string blobName, string? originalFileName,
        string contentType, long sizeBytes, string? caption,
        CancellationToken ct = default)
    {
        var maxOrder = await BuildBaseQuery()
            .Where(p => p.PlaceId == placeId)
            .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

        var photo = new PlacePhoto
        {
            PlaceId = placeId,
            UserId = _userContext.UserId!.Value,
            BlobName = blobName,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Caption = caption,
            SortOrder = maxOrder + 1,
            UploadedAt = DateTime.UtcNow,
        };
        await InsertAsync(photo, ct);
        return new PlacePhotoDto(photo.Id, photo.PlaceId, photo.UserId, photo.OriginalFileName,
            photo.ContentType, photo.SizeBytes, photo.Caption, photo.SortOrder, photo.UploadedAt, 0, 0);
    }

    public Task<List<PlacePhotoDto>> GetByPlaceAsync(int placeId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.PlaceId == placeId && p.UserId == _userContext.UserId)
            .OrderBy(p => p.SortOrder)
            .GroupJoin(
                Context.Set<PhotoRating>().AsNoTracking(),
                p => p.Id,
                r => r.PhotoId,
                (p, ratings) => new { p, ratings })
            .Select(x => new PlacePhotoDto(
                x.p.Id, x.p.PlaceId, x.p.UserId, x.p.OriginalFileName,
                x.p.ContentType, x.p.SizeBytes, x.p.Caption, x.p.SortOrder, x.p.UploadedAt,
                x.ratings.Any() ? x.ratings.Average(r => (double)r.Rating) : 0,
                x.ratings.Count()))
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(int photoId, CancellationToken ct = default)
    {
        var rows = await ExecuteDeleteAsync(p => p.Id == photoId && p.UserId == _userContext.UserId, ct);
        return rows > 0;
    }

    public async Task RateAsync(int photoId, byte rating, CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var existing = await Context.Set<PhotoRating>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PhotoId == photoId, ct);

        if (existing is null)
        {
            Context.Set<PhotoRating>().Add(new PhotoRating
            {
                UserId = userId,
                PhotoId = photoId,
                Rating = rating,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Rating = rating;
        }
        await Context.SaveChangesAsync(ct);
    }

    public Task<PlacePhotoBlobInfo?> GetBlobInfoAsync(int photoId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.Id == photoId)
            .Select(p => new PlacePhotoBlobInfo(p.Id, p.BlobName, p.ContentType))
            .FirstOrDefaultAsync(ct);
}
