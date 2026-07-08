using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Blobs;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Features.Dashboard;
using EnergyTracker.Api.Features.Flats;
using EnergyTracker.Api.Features.FlatStructure;
using EnergyTracker.Api.Features.Readings;
using EnergyTracker.Api.Features.Onboarding;
using EnergyTracker.Api.Features.SmartPlugImport;
using EnergyTracker.Api.Features.Tariffs;
using EnergyTracker.Api.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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

var storageAccountName = builder.Configuration["AzureStorageAccountName"]
    ?? throw new InvalidOperationException("Required configuration 'AzureStorageAccountName' is missing.");
var managedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"];
var storageCredential = managedIdentityClientId is not null
    ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentityClientId })
    : new DefaultAzureCredential();
builder.Services.AddSingleton(new BlobServiceClient(
    new Uri($"https://{storageAccountName}.blob.core.windows.net"), storageCredential));

builder.Services.AddSingleton<LocaleResolver>();
builder.Services.AddSingleton<OnboardingValidator>();
builder.Services.AddSingleton<PatchFlatValidator>();
builder.Services.AddSingleton<CreateFlatValidator>();
builder.Services.AddSingleton<ReadingValidator>();
builder.Services.AddSingleton<PatchReadingValidator>();
builder.Services.AddSingleton<TariffValidator>();
builder.Services.AddSingleton<PatchTariffValidator>();
builder.Services.AddScoped<TariffResolver>();
builder.Services.AddScoped<EveHomeParser>();
builder.Services.AddScoped<MerossParser>();
builder.Services.AddScoped<InterpolationEngine>();
builder.Services.AddScoped<ReconciliationEngine>();
builder.Services.AddSingleton<KpiCalculator>();
builder.Services.AddSingleton<UpdateFlatStructureValidator>();

JsonSerializationDefaults.ConfigureAspNetCoreJsonOptions(builder.Services);

builder.Build().Run();
