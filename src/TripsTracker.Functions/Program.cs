using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TripsTracker.Data;
using TripsTracker.Data.Registration;
using TripsTracker.Functions.Middleware;
using TripsTracker.Integration.Registration;
using TripsTracker.Interfaces;
using TripsTracker.Interfaces.Configuration;
using TripsTracker.Tools.Registration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<JwtValidationMiddleware>();

// Register strongly-typed configuration options
builder.Services.AddApplicationOptions(builder.Configuration);

// Register database context
var dbOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();
builder.Services.AddDatabaseContext<TripsTrackerDbContext>(dbOptions);

// Register typed HTTP clients for integration services (before Scrutor so TryAdd skips them)
builder.Services.AddIntegrationHttpClients();

// IHttpContextAccessor — required by JwtValidationMiddleware to read the Bearer token
builder.Services.AddHttpContextAccessor();

// JWT validator (singleton — holds OIDC discovery config + signing key cache)
builder.Services.AddSingleton<JwtValidatorService>();

// UserContext — scoped per request; registered as both concrete type (for middleware) and interface (for business classes)
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<UserContext>());

// Auto-register all application services against their interfaces (after manual registrations so TryAdd skips them)
builder.Services.AddScopedApplicationServices("TripsTracker.");

builder.Build().Run();
