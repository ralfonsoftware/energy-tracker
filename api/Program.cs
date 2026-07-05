using Azure.Monitor.OpenTelemetry.Exporter;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Features.Dashboard;
using EnergyTracker.Api.Features.Flats;
using EnergyTracker.Api.Features.FlatStructure;
using EnergyTracker.Api.Features.Readings;
using EnergyTracker.Api.Features.Onboarding;
using EnergyTracker.Api.Features.Tariffs;
using EnergyTracker.Api.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<TenantResolverMiddleware>();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

var sqlConnectionString = builder.Configuration["SqlConnectionString"]
    ?? throw new InvalidOperationException("Required configuration 'SqlConnectionString' is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString));

builder.Services.AddSingleton<LocaleResolver>();
builder.Services.AddSingleton<OnboardingValidator>();
builder.Services.AddSingleton<PatchFlatValidator>();
builder.Services.AddSingleton<CreateFlatValidator>();
builder.Services.AddSingleton<ReadingValidator>();
builder.Services.AddSingleton<PatchReadingValidator>();
builder.Services.AddSingleton<TariffValidator>();
builder.Services.AddSingleton<PatchTariffValidator>();
builder.Services.AddScoped<TariffResolver>();
builder.Services.AddSingleton<KpiCalculator>();
builder.Services.AddSingleton<UpdateFlatStructureValidator>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Build().Run();
