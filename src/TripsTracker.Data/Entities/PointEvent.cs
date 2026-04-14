using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Data.Entities;

public class PointEvent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(50)] public string EventType { get; set; } = string.Empty;
    public int Points { get; set; }
    public int? ReferenceId { get; set; }
    [MaxLength(50)] public string? ReferenceType { get; set; }
    public DateTime CreatedAt { get; set; }
}
