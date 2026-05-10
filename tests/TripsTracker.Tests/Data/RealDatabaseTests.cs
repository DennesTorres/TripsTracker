using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Transactions;
using TripsTracker.Business;
using TripsTracker.Data;
using TripsTracker.Data.Entities;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;

namespace TripsTracker.Tests.Data;

/// <summary>
/// IUserContext backed by the seeded owner (dennes@bufaloinfo.com.br).
/// Looked up from the database so the test is not coupled to a hardcoded int ID.
/// </summary>
file sealed class OwnerUserContext : IUserContext
{
    public int? UserId { get; }
    public string? Email { get; }
    public bool IsAuthenticated => UserId is not null;

    public OwnerUserContext(int userId, string email)
    {
        UserId = userId;
        Email = email;
    }
}

/// <summary>
/// Integration tests that hit the real SQL Server database.
/// No mocking — business classes run against the actual schema and data.
/// Write tests use a TransactionScope that rolls back on dispose.
/// </summary>
[TestClass]
public class RealDatabaseTests
{
    #region Fixture

    private sealed class Fixture : IAsyncDisposable
    {
        public TripsTrackerDbContext Context { get; }
        public PlaceBusiness Places { get; }
        public CountryBusiness Countries { get; }
        public VisitedStateBusiness States { get; }
        public int OwnerUserId { get; }
        private TransactionScope? _scope;

        public Fixture()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var dbOptions = config.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                ?? throw new InvalidOperationException($"'{DatabaseOptions.SectionName}' configuration section is missing.");

            var options = new DbContextOptionsBuilder<TripsTrackerDbContext>()
                .UseSqlServer(dbOptions.ConnectionString)
                .Options;

            Context = new TripsTrackerDbContext(options);

            // Look up the seeded owner — tests run as this user
            var owner = Context.Set<User>().First(u => u.Email == "dennes@bufaloinfo.com.br");
            OwnerUserId = owner.Id;
            var userContext = new OwnerUserContext(owner.Id, owner.Email);

            Places = new PlaceBusiness(Context, userContext);
            Countries = new CountryBusiness(Context, userContext);
            States = new VisitedStateBusiness(Context, userContext);
        }

        public void BeginScope()
        {
            _scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);
            Context.Database.OpenConnection(); // keep single connection enlisted — prevents DTC escalation
        }

        public async ValueTask DisposeAsync()
        {
            if (_scope != null)
                Context.Database.CloseConnection();
            await Context.DisposeAsync();
            _scope?.Dispose(); // no Complete() → automatic rollback
        }
    }

    #endregion

    #region Countries

    [TestMethod]
    public async Task CountryBusiness_GetAllAsync_ReturnsAllSeededCountries()
    {
        await using var f = new Fixture();

        var countries = await f.Countries.GetAllAsync();

        Assert.IsGreaterThanOrEqualTo(195, countries.Count, $"Expected >= 195 countries, got {countries.Count}.");
    }

    [TestMethod]
    public async Task CountryBusiness_GetAllAsync_AllCountriesHaveIsoAlpha2()
    {
        await using var f = new Fixture();

        var countries = await f.Countries.GetAllAsync();

        Assert.IsTrue(countries.All(c => !string.IsNullOrEmpty(c.IsoAlpha2)),
            "Every country must have an IsoAlpha2 code.");
    }

    #endregion

    #region Places

    [TestMethod]
    public async Task PlaceBusiness_GetAllAsync_AllPlacesHaveCountryResolved()
    {
        await using var f = new Fixture();

        var places = await f.Places.GetAllAsync();

        Assert.IsNotEmpty(places, "Expected at least one place.");
        Assert.IsTrue(places.All(p => p.CountryId > 0),
            "Every place must have a resolved CountryId.");
        Assert.IsTrue(places.All(p => !string.IsNullOrEmpty(p.CountryName)),
            "Every place must have a resolved country name.");
    }

    [TestMethod]
    public async Task PlaceBusiness_GetAllAsync_StateAbbrIsShortWhenPresent()
    {
        await using var f = new Fixture();

        var places = await f.Places.GetAllAsync();

        var invalid = places.Where(p => p.StateAbbr != null && p.StateAbbr.Length > 3).ToList();
        Assert.IsEmpty(invalid,
            $"Found places with StateAbbr longer than 3 chars: {string.Join(", ", invalid.Select(p => $"{p.City}({p.StateAbbr})"))}");
    }

    [TestMethod]
    public async Task PlaceBusiness_CreateAndRead_RollsBackWithoutAffectingRealData()
    {
        await using var f = new Fixture();
        f.BeginScope();

        var brazil = f.Context.Set<Country>().First(c => c.IsoAlpha2 == "BR");
        var testPlace = new Place
        {
            City = "__TEST_CITY__",
            CountryId = brazil.Id,
            Lat = -23.5,
            Lon = -46.6,
            IsHome = false,
            UserId = f.OwnerUserId,
        };
        f.Context.Set<Place>().Add(testPlace);
        await f.Context.SaveChangesAsync();

        var found = await f.Places.GetAllAsync();
        Assert.IsTrue(found.Any(p => p.City == "__TEST_CITY__"), "Test place should be visible within the transaction.");

        // Fixture.DisposeAsync disposes scope without Complete() → rolls back
    }

    #endregion

    #region VisitedStates view

    [TestMethod]
    public async Task VisitedStateBusiness_GetAllAsync_ViewReturnsStatesFromPlaces()
    {
        await using var f = new Fixture();

        var states = await f.States.GetAllAsync();
        var places = await f.Places.GetAllAsync();

        var placesWithState = places.Where(p => !string.IsNullOrEmpty(p.StateAbbr)).ToList();

        if (placesWithState.Count == 0)
            Assert.Inconclusive("No places with StateAbbr in database — cannot verify view.");

        Assert.IsNotEmpty(states, "VisitedStates view must return at least one row when places have StateAbbr.");
        Assert.IsTrue(states.All(s => s.CountryId > 0), "All view rows must have a valid CountryId.");
        Assert.IsTrue(states.All(s => !string.IsNullOrEmpty(s.StateAbbr)), "All view rows must have a StateAbbr.");

        // Every state in the view must correspond to at least one place with that StateAbbr+CountryId
        foreach (var state in states)
        {
            var match = placesWithState.Any(p => p.CountryId == state.CountryId && p.StateAbbr == state.StateAbbr);
            Assert.IsTrue(match,
                $"VisitedStates view row (CountryId={state.CountryId}, StateAbbr={state.StateAbbr}) has no matching Place.");
        }
    }

    [TestMethod]
    public async Task VisitedStateBusiness_GetAllAsync_BrazilHasStates()
    {
        await using var f = new Fixture();

        var brazil = f.Context.Set<TripsTracker.Data.Entities.Country>()
            .First(c => c.IsoAlpha2 == "BR");

        var states = await f.States.GetAllAsync();
        var brazilStates = states.Where(s => s.CountryId == brazil.Id).ToList();

        Assert.IsNotEmpty(brazilStates,
            "Brazil must have at least one state in the VisitedStates view. " +
            "If this fails, Brazilian places are missing StateAbbr — check migration data extraction.");
    }

    #endregion
}
