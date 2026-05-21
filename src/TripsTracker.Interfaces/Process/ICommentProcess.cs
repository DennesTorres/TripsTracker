using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface ICommentProcess
{
    Task<PlaceCommentDto> CreateAsync(int placeId, string text, CancellationToken ct = default);
}
