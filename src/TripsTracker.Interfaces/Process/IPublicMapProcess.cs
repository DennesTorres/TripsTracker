using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Process;

public interface IPublicMapProcess
{
    Task<PublicMapDto?> GetSharedMapAsync(string token, CancellationToken ct = default);
}
