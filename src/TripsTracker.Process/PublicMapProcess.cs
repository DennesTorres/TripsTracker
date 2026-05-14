using TripsTracker.Domain;
using TripsTracker.Interfaces.Business;
using TripsTracker.Interfaces.Process;

namespace TripsTracker.Process;

public class PublicMapProcess : IPublicMapProcess
{
    private readonly IShareLinkBusiness _shareLinks;
    private readonly IPlaceBusiness _places;
    private readonly ICountryBusiness _countries;
    private readonly IVisitedStateBusiness _states;
    private readonly IUserBusiness _users;

    public PublicMapProcess(
        IShareLinkBusiness shareLinks,
        IPlaceBusiness places,
        ICountryBusiness countries,
        IVisitedStateBusiness states,
        IUserBusiness users)
    {
        _shareLinks = shareLinks;
        _places = places;
        _countries = countries;
        _states = states;
        _users = users;
    }

    public async Task<PublicMapDto?> GetSharedMapAsync(string token, CancellationToken ct = default)
    {
        var link = await _shareLinks.GetByTokenAsync(token, ct);
        if (link is null) return null;

        await _shareLinks.IncrementViewCountAsync(token, ct);

        // Fetch owner's data using the OwnerId already present on the share link
        var owner = await _users.GetByIdAsync(link.OwnerId, ct);
        var places = await _places.GetAllForUserAsync(link.OwnerId, ct);
        var countries = await _countries.GetAllForUserAsync(link.OwnerId, ct);
        var states = await _states.GetAllForUserAsync(link.OwnerId, ct);

        return new PublicMapDto(
            owner?.DisplayName ?? owner?.Email ?? "Traveler",
            places,
            countries,
            states);
    }
}
