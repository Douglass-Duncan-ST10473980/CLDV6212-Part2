// Authors: Tahir Ismail, Douglass Duncan (ST10473980), Neha

//Author: Tahir Ismail , Douglass Duncan, Neha Heeralal
using Azure.Monitor.OpenTelemetry.Exporter;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Register the service responsible for accessing the MenuItems table.
// Register MenuStorageService with logger support
builder.Services.AddSingleton<MenuStorageService>(serviceProvider =>
{
    string connectionString =
        Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";

    var logger =
        serviceProvider.GetRequiredService<ILogger<MenuStorageService>>();

    return new MenuStorageService(connectionString, logger);
});

// Register the service responsible for placing orders onto the
// order-processing-queue.
builder.Services.AddSingleton<OrderQueueService>();
// Register OrderStorageService with logger support
builder.Services.AddSingleton<OrderStorageService>(serviceProvider =>
{
    string connectionString =
        Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";

    var logger =
        serviceProvider.GetRequiredService<ILogger<OrderStorageService>>();

    return new OrderStorageService(connectionString, logger);
});

if (!string.IsNullOrEmpty(
        Environment.GetEnvironmentVariable(
            "APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}


builder.ConfigureFunctionsWebApplication();

builder.Build().Run();