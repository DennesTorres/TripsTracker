using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IPlaceCommentBusiness
{
    Task<PlaceCommentDto> CreateAsync(int placeId, string text, CancellationToken ct = default);
    Task<PlaceCommentDto> CreateReplyAsync(int parentCommentId, string text, CancellationToken ct = default);
    Task<List<PlaceCommentDto>> GetByPlaceAsync(int placeId, CancellationToken ct = default);
    Task<PlaceCommentDto?> UpdateAsync(int commentId, string text, CancellationToken ct = default);
    Task<bool> DeleteAsync(int commentId, CancellationToken ct = default);
    Task VoteAsync(int commentId, bool isUpvote, CancellationToken ct = default);
}
