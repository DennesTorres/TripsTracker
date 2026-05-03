using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPlacePhotoBusiness
{
    Task<PlacePhotoDto> CreateAsync(int placeId, string blobName, string? originalFileName, string contentType, long sizeBytes, string? caption, CancellationToken ct = default);
    Task<List<PlacePhotoDto>> GetByPlaceAsync(int placeId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int photoId, CancellationToken ct = default);
    Task RateAsync(int photoId, byte rating, CancellationToken ct = default);
    Task<PlacePhotoBlobInfo?> GetBlobInfoAsync(int photoId, CancellationToken ct = default);
}
