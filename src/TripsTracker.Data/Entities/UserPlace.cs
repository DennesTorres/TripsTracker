namespace TripsTracker.Data.Entities;

public class UserPlace
{
    public int UserId { get; set; }
    public int PlaceId { get; set; }
    public bool IsHome { get; set; }
}
