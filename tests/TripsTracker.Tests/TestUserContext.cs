using TripsTracker.Interfaces;

namespace TripsTracker.Tests;

internal sealed class TestUserContext(int userId) : IUserContext
{
    public int? UserId => userId;
    public string? Email => null;
    public bool IsAuthenticated => true;
}
