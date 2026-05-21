using TripsTracker.Domain;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Integration;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class PhotoProcess : IPhotoProcess
{
    private readonly IPlacePhotoBusiness _photos;
    private readonly IBlobStorageService _blobs;
    private readonly IUserContext _userContext;

    public PhotoProcess(IPlacePhotoBusiness photos, IBlobStorageService blobs, IUserContext userContext)
    {
        _photos = photos;
        _blobs = blobs;
        _userContext = userContext;
    }

    public async Task<PlacePhotoDto> UploadAsync(int placeId, Stream stream, string contentType, string fileName, long sizeBytes, string? caption, CancellationToken ct = default)
    {
        var userId = _userContext.UserId!.Value;
        var ext = Path.GetExtension(fileName);
        var blobName = $"{userId}/{placeId}/{Guid.NewGuid()}{ext}";

        await _blobs.UploadAsync(blobName, stream, contentType, ct);
        return await _photos.CreateAsync(placeId, blobName, fileName, contentType, sizeBytes, caption, ct);
    }

    public async Task<bool> DeleteAsync(int photoId, CancellationToken ct = default)
    {
        var blobInfo = await _photos.GetBlobInfoAsync(photoId, ct);
        if (blobInfo is not null)
            await _blobs.DeleteAsync(blobInfo.BlobName, ct);

        return await _photos.DeleteAsync(photoId, ct);
    }
}
