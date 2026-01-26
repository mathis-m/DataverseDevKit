using System.CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DataverseDevKit.PluginHost.Runtime;
using DataverseDevKit.PluginHost.Services;

// Define command line options
var socketOption = new Option<string>(
    name: "--socket",
    description: "Path to the Unix domain socket for gRPC communication")
{ IsRequired = true };

var assemblyOption = new Option<string>(
    name: "--assembly",
    description: "Path to the plugin assembly to load")
{ IsRequired = true };

var pluginIdOption = new Option<string>(
    name: "--plugin-id",
    description: "The plugin ID")
{ IsRequired = true };

var instanceIdOption = new Option<string>(
    name: "--instance-id",
    description: "The instance ID for isolation")
{ IsRequired = true };

var rootCommand = new RootCommand("DataverseDevKit Plugin Host - Out-of-process plugin runtime")
{
    socketOption,
    assemblyOption,
    pluginIdOption,
    instanceIdOption
};

rootCommand.SetHandler(async (socket, assembly, pluginId, instanceId) =>
{
    await RunPluginHostAsync(socket, assembly, pluginId, instanceId);
}, socketOption, assemblyOption, pluginIdOption, instanceIdOption);

return await rootCommand.InvokeAsync(args);

static async Task RunPluginHostAsync(string socketPath, string assemblyPath, string pluginId, string instanceId)
{
    // Ensure socket directory exists
    var socketDir = Path.GetDirectoryName(socketPath);
    if (!string.IsNullOrEmpty(socketDir) && !Directory.Exists(socketDir))
    {
        Directory.CreateDirectory(socketDir);
    }

    // Remove existing socket file
    if (File.Exists(socketPath))
    {
        File.Delete(socketPath);
    }

    // Load the plugin assembly
    var pluginLoader = new PluginLoader();
    await pluginLoader.LoadPluginAsync(assemblyPath);

    // Build and configure web application for gRPC
    var builder = WebApplication.CreateBuilder();

    builder.Logging.SetMinimumLevel(LogLevel.Information);
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddGrpc();
    builder.Services.AddGrpcReflection();
    builder.Services.AddSingleton(pluginLoader);
    
    // Register the mock ServiceClientFactory
    // In a real scenario, this would create actual Dataverse SDK clients
    builder.Services.AddSingleton<DataverseDevKit.Core.Abstractions.IServiceClientFactory>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<MockServiceClientFactory>>();
        return new MockServiceClientFactory(logger);
    });
    
    builder.Services.AddSingleton<PluginHostGrpcService>();

    // Configure Kestrel for Unix domain socket
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenUnixSocket(socketPath, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    });

    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Plugin Host starting for plugin: {PluginId} (instance: {InstanceId})", pluginId, instanceId);
    logger.LogInformation("Socket path: {SocketPath}", socketPath);
    logger.LogInformation("Assembly: {AssemblyPath}", assemblyPath);

    app.MapGrpcService<PluginHostGrpcService>();
    app.MapGrpcReflectionService();

    // Start the server
    await app.StartAsync();

    // Signal that we're ready
    Console.WriteLine("READY");
    Console.Out.Flush();

    logger.LogInformation("Plugin Host ready and listening");

    // Wait for shutdown
    await app.WaitForShutdownAsync();

    // Cleanup
    await pluginLoader.DisposeAsync();
}
