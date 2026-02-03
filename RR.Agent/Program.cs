using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build the host with configuration and services
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();