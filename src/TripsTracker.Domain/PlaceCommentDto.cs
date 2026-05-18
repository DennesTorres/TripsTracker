namespace TripsTracker.Domain;

public record PlaceCommentDto(
    int Id, int PlaceId, int UserId, string UserDisplayName,
    string Text, DateTime CreatedAt, DateTime? UpdatedAt,
    int UpvoteCount, int DownvoteCount);

public record CreateCommentDto(string Text);
