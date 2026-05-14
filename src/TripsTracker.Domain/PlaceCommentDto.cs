namespace TripsTracker.Domain;

public class PlaceCommentDto
{
    public int Id { get; init; }
    public int PlaceId { get; init; }
    public int UserId { get; init; }
    public required string UserDisplayName { get; init; }
    public required string Text { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int UpvoteCount { get; init; }
    public int DownvoteCount { get; init; }
    public int? ParentCommentId { get; init; }
}

public class CreateCommentDto
{
    public required string Text { get; init; }
}

public class CreateReplyDto
{
    public required string Text { get; init; }
}
