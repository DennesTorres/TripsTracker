namespace TripsTracker.Domain;

public record PlacePhotoDto(
    int Id, int PlaceId, int UserId, string? OriginalFileName,
    string ContentType, long SizeBytes, string? Caption,
    int SortOrder, DateTime UploadedAt, double AverageRating, int RatingCount);

public record PlacePhotoBlobInfo(int Id, string BlobName, string ContentType);
