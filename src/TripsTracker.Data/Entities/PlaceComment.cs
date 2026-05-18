using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Data.Entities;

public class PlaceComment
{
    public int Id { get; set; }
    public int PlaceId { get; set; }
    public int UserId { get; set; }
    public int? ParentCommentId { get; set; }
    [MaxLength(2000)] public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
