using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IPhotoProcess
{
    Task<PlacePhotoDto> UploadAsync(int placeId, Stream stream, string contentType, string fileName, long sizeBytes, string? caption, CancellationToken ct = default);
    Task<bool> DeleteAsync(int photoId, CancellationToken ct = default);
}
