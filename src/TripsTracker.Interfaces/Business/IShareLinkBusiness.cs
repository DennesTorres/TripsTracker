using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IShareLinkBusiness
{
    Task<ShareLinkDto> CreateAsync(CreateShareLinkDto dto, CancellationToken ct = default);
    Task<List<ShareLinkDto>> GetUserLinksAsync(CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, CancellationToken ct = default);
    Task<ShareLinkDto?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task IncrementViewCountAsync(string token, CancellationToken ct = default);
    Task<int?> GetOwnerIdAsync(string token, CancellationToken ct = default);
}
