using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IAddPlaceProcess
{
    Task<PlaceDto> ExecuteAsync(AddPlaceDto dto, CancellationToken ct = default);
}
