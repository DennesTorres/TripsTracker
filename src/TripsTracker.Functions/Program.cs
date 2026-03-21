using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TripsTracker.Data;
using TripsTracker.Data.Registration;
using TripsTracker.Integration.Registration;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Tools.Registration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();


// Register strongly-typed configuration options
builder.Services.AddApplicationOptions(builder.Configuration);

// Register database context
var dbOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();
builder.Services.AddDatabaseContext<TripsTrackerDbContext>(dbOptions);

// Register typed HTTP clients for integration services (before Scrutor so TryAdd skips them)
builder.Services.AddIntegrationHttpClients();

// Auto-register all application services against their interfaces
builder.Services.AddScopedApplicationServices("TripsTracker.");

builder.Build().Run();
