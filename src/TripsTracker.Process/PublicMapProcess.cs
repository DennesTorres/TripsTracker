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

        // Get owner's UserId from the share link
        var ownerId = await _shareLinks.GetOwnerIdAsync(token, ct);
        if (ownerId is null) return null;

        // Fetch owner's data using Business layer methods that accept an explicit userId
        var owner = await _users.GetByIdAsync(ownerId.Value, ct);
        var places = await _places.GetAllForUserAsync(ownerId.Value, ct);
        var countries = await _countries.GetAllForUserAsync(ownerId.Value, ct);
        var states = await _states.GetAllForUserAsync(ownerId.Value, ct);

        return new PublicMapDto(
            owner?.DisplayName ?? owner?.Email ?? "Traveler",
            places,
            countries,
            states);
    }
}
