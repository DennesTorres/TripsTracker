namespace TripsTracker.Data.Entities;

public class CommentRating
{
    public int UserId { get; set; }
    public int CommentId { get; set; }
    public bool IsUpvote { get; set; }
    public DateTime CreatedAt { get; set; }
}
