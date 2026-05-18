using TripsTracker.Domain;

namespace TripsTracker.Interfaces.Business;

public interface IExploreBusiness
{
    Task<List<ExploreLocationDto>> SearchAsync(string query, CancellationToken ct = default);
    Task<ExploreContentDto> GetContentAsync(string city, int countryId, CancellationToken ct = default);
}
