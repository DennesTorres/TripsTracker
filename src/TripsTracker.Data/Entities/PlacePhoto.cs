using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Data.Entities;

public class PlacePhoto
{
    public int Id { get; set; }
    public int PlaceId { get; set; }
    public int UserId { get; set; }
    [MaxLength(256)] public string BlobName { get; set; } = string.Empty;
    [MaxLength(256)] public string? OriginalFileName { get; set; }
    [MaxLength(100)] public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    [MaxLength(500)] public string? Caption { get; set; }
    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; }
}
