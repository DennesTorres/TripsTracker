using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripsTracker.Tools.Registration;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register strongly-typed configuration options
builder.Services.AddApplicationOptions(builder.Configuration);

// Auto-register all application services against their interfaces
builder.Services.AddScopedApplicationServices("TripsTracker.");

builder.Build().Run();
