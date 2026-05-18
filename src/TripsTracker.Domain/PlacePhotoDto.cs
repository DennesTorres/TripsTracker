namespace TripsTracker.Domain;

public class PlacePhotoDto
{
    public int Id { get; init; }
    public int PlaceId { get; init; }
    public int UserId { get; init; }
    public string? OriginalFileName { get; init; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string? Caption { get; init; }
    public int SortOrder { get; init; }
    public DateTime UploadedAt { get; init; }
    public double AverageRating { get; init; }
    public int RatingCount { get; init; }
    public int? CurrentUserRating { get; init; }
}

public class PlacePhotoBlobInfo
{
    public int Id { get; init; }
    public required string BlobName { get; init; }
    public required string ContentType { get; init; }
}
