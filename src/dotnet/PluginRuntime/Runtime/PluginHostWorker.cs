using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using DataverseDevKit.PluginHost.Services;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Background service that hosts the gRPC server for plugin communication.
/// </summary>
public class PluginHostWorker : BackgroundService
{
    private readonly ILogger<PluginHostWorker> _logger;
    private readonly PluginLoader _pluginLoader;
    private WebApplication? _app;

    public PluginHostWorker(ILogger<PluginHostWorker> logger, PluginLoader pluginLoader)
    {
        _logger = logger;
        _pluginLoader = pluginLoader;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Get transport configuration
            var transport = Environment.GetEnvironmentVariable("DDK_TRANSPORT") ?? "uds";
            var pluginId = Environment.GetEnvironmentVariable("DDK_PLUGIN_ID") ?? "unknown";
            var pid = Environment.ProcessId;

            _logger.LogInformation("Starting plugin host worker for plugin: {PluginId}, PID: {Pid}", pluginId, pid);

            // Load the plugin assembly
            var assemblyPath = Environment.GetEnvironmentVariable("DDK_PLUGIN_ASSEMBLY");
            if (string.IsNullOrEmpty(assemblyPath))
            {
                throw new InvalidOperationException("DDK_PLUGIN_ASSEMBLY environment variable not set");
            }

            await _pluginLoader.LoadPluginAsync(assemblyPath, stoppingToken);

            // Determine socket path first
            var socketPath = OperatingSystem.IsWindows()
                ? Path.Combine(Path.GetTempPath(), $"ddk-{pid}-{pluginId}.sock")
                : $"/tmp/ddk/{pid}/{pluginId}.sock";

            // Ensure directory exists
            var dir = Path.GetDirectoryName(socketPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Remove existing socket file
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }

            _logger.LogInformation("Will listen on Unix domain socket: {SocketPath}", socketPath);
            
            // Write socket path to stdout IMMEDIATELY for parent process to read
            Console.WriteLine($"SOCKET_PATH={socketPath}");
            Console.Out.Flush();

            // Build and configure web application for gRPC
            var builder = WebApplication.CreateBuilder();
            
            builder.Services.AddGrpc();
            builder.Services.AddGrpcReflection();
            builder.Services.AddSingleton(_pluginLoader);
            builder.Services.AddSingleton<PluginHostGrpcService>();

            // Configure Kestrel for appropriate transport
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenUnixSocket(socketPath, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });
            
            _logger.LogInformation("Kestrel configured for Unix domain socket: {SocketPath}", socketPath);

            _app = builder.Build();

            _app.MapGrpcService<PluginHostGrpcService>();
            _app.MapGrpcReflectionService();

            await _app.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in plugin host worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping plugin host worker");
        
        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
