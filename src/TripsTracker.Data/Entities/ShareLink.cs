using System.ComponentModel.DataAnnotations;

namespace TripsTracker.Data.Entities;

public class ShareLink
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(32)] public string Token { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool RequiresLogin { get; set; }
    public bool IsDiscoverable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int ViewCount { get; set; }
}
