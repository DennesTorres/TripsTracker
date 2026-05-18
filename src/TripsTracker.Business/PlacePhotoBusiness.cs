using Microsoft.EntityFrameworkCore;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Exceptions;

namespace TripsTracker.Business;

public class PlacePhotoBusiness : BusinessBase<PlacePhoto>, IPlacePhotoBusiness
{
    private const long StorageQuotaBytes = 100L * 1024 * 1024; // 100 MB per user

    private readonly IUserContext _userContext;
    private readonly IUserBusiness _users;

    public PlacePhotoBusiness(TripsTrackerDbContext context, IUserContext userContext, IUserBusiness users) : base(context)
    {
        _userContext = userContext;
        _users = users;
    }

    public async Task<PlacePhotoDto> CreateAsync(
        int placeId, string blobName, string? originalFileName,
        string contentType, long sizeBytes, string? caption,
        CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var used = await _users.GetStorageUsedAsync(userId, ct);
        if (used + sizeBytes > StorageQuotaBytes)
            throw new BusinessRuleException("Storage quota exceeded (100 MB per user).");

        var maxOrder = await BuildBaseQuery()
            .Where(p => p.PlaceId == placeId)
            .MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;

        var photo = new PlacePhoto
        {
            PlaceId = placeId,
            UserId = userId,
            BlobName = blobName,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Caption = caption,
            SortOrder = maxOrder + 1,
            UploadedAt = DateTime.UtcNow,
        };
        await InsertAsync(photo, ct);
        await _users.AddStorageUsedAsync(userId, sizeBytes, ct);
        return new PlacePhotoDto { Id = photo.Id, PlaceId = photo.PlaceId, UserId = photo.UserId, OriginalFileName = photo.OriginalFileName, ContentType = photo.ContentType, SizeBytes = photo.SizeBytes, Caption = photo.Caption, SortOrder = photo.SortOrder, UploadedAt = photo.UploadedAt };
    }

    public Task<List<PlacePhotoDto>> GetByPlaceAsync(int placeId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.PlaceId == placeId)
            .OrderBy(p => p.SortOrder)
            .GroupJoin(
                Context.Set<PhotoRating>().AsNoTracking(),
                p => p.Id,
                r => r.PhotoId,
                (p, ratings) => new { p, ratings })
            .Select(x => new PlacePhotoDto { Id = x.p.Id, PlaceId = x.p.PlaceId, UserId = x.p.UserId, OriginalFileName = x.p.OriginalFileName, ContentType = x.p.ContentType, SizeBytes = x.p.SizeBytes, Caption = x.p.Caption, SortOrder = x.p.SortOrder, UploadedAt = x.p.UploadedAt, AverageRating = x.ratings.Any() ? x.ratings.Average(r => (double)r.Rating) : 0, RatingCount = x.ratings.Count() })
            .ToListAsync(ct);

    public async Task<bool> DeleteAsync(int photoId, CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var size = await BuildBaseQuery()
            .Where(p => p.Id == photoId && p.UserId == userId)
            .Select(p => (long?)p.SizeBytes)
            .FirstOrDefaultAsync(ct);

        if (size is null) return false;

        var rows = await ExecuteDeleteAsync(p => p.Id == photoId && p.UserId == userId, ct);
        if (rows > 0)
            await _users.AddStorageUsedAsync(userId, -size.Value, ct);
        return rows > 0;
    }

    public async Task RateAsync(int photoId, byte rating, CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var existing = await Context.Set<PhotoRating>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PhotoId == photoId, ct);
        if (existing != null)
        {
            existing.Rating = rating;
            await Context.SaveChangesAsync(ct);
        }
        else
        {
            try
            {
                Context.Set<PhotoRating>().Add(new PhotoRating
                {
                    UserId = userId,
                    PhotoId = photoId,
                    Rating = rating,
                    CreatedAt = DateTime.UtcNow,
                });
                await Context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException) { /* concurrent insert -- already rated */ }
        }
    }

    public Task<PlacePhotoBlobInfo?> GetBlobInfoAsync(int photoId, CancellationToken ct = default)
        => BuildBaseQuery()
            .Where(p => p.Id == photoId)
            .Select(p => new PlacePhotoBlobInfo { Id = p.Id, BlobName = p.BlobName, ContentType = p.ContentType })
            .FirstOrDefaultAsync(ct);
}
