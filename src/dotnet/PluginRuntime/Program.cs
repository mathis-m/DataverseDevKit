using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DataverseDevKit.PluginHost.Runtime;

var builder = Host.CreateApplicationBuilder(args);

// gRPC services
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Plugin hosting
builder.Services.AddSingleton<PluginLoader>();
builder.Services.AddHostedService<PluginHostWorker>();

var host = builder.Build();

// Configure transport based on platform
var transportConfig = Environment.GetEnvironmentVariable("DDK_TRANSPORT") ?? string.Empty;
var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Plugin Host starting with transport: {Transport}", transportConfig);

await host.RunAsync();
