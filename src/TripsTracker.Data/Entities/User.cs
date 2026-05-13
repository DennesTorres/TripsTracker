namespace TripsTracker.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDiscoverable { get; set; }
    public long StorageUsedBytes { get; set; }
    public DateTime? StorageLastRefreshedAt { get; set; }
}
