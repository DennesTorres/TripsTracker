namespace TripsTracker.Data.Entities;

public class PhotoRating
{
    public int UserId { get; set; }
    public int PhotoId { get; set; }
    public byte Rating { get; set; } // 1-5
    public DateTime CreatedAt { get; set; }
}
