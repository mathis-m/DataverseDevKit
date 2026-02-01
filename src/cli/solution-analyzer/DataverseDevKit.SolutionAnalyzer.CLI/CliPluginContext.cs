using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Simple plugin context implementation for CLI usage
/// </summary>
internal class CliPluginContext : IPluginContext
{
    private readonly ILogger _logger;
    private readonly string _storagePath;
    private readonly IServiceClientFactory _serviceClientFactory;

    public CliPluginContext(ILogger logger, string storagePath, IServiceClientFactory serviceClientFactory)
    {
        _logger = logger;
        _storagePath = storagePath;
        _serviceClientFactory = serviceClientFactory;
    }

    public ILogger Logger => _logger;

    public string StoragePath => _storagePath;

    public IServiceClientFactory ServiceClientFactory => _serviceClientFactory;

    public void EmitEvent(PluginEvent pluginEvent)
    {
        // In CLI mode, we just log events instead of emitting them to a host
        _logger.LogDebug("Plugin Event: {EventType} - {Message}", 
            pluginEvent.Type, pluginEvent.Message);
    }

    public Task<string?> GetConfigAsync(string key, CancellationToken cancellationToken = default)
    {
        // CLI doesn't use persistent config
        return Task.FromResult<string?>(null);
    }

    public Task SetConfigAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        // CLI doesn't use persistent config
        return Task.CompletedTask;
    }
}
