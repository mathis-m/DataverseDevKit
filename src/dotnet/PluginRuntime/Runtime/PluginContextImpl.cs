using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;
using System.Text.Json;

namespace DataverseDevKit.PluginHost.Runtime;

/// <summary>
/// Implementation of IPluginContext for the plugin runtime.
/// </summary>
internal class PluginContextImpl : IPluginContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger;
    private readonly string _storagePath;
    private readonly string _pluginId;
    private readonly List<PluginEvent> _events = new();
    private readonly IServiceClientFactory _serviceClientFactory;

    public PluginContextImpl(ILogger logger, string storagePath, string pluginId, IServiceClientFactory serviceClientFactory)
    {
        _logger = logger;
        _storagePath = storagePath;
        _pluginId = pluginId;
        _serviceClientFactory = serviceClientFactory;
        
        // Ensure storage directory exists
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public ILogger Logger => _logger;
    public string StoragePath => _storagePath;
    public IServiceClientFactory ServiceClientFactory => _serviceClientFactory;

    public IReadOnlyList<PluginEvent> Events => _events.AsReadOnly();

    public void EmitEvent(PluginEvent @event)
    {
        _events.Add(@event);
        _logger.LogDebug("Event emitted: {EventType}", @event.Type);
    }

    /// <summary>
    /// Convenience method to emit an event with automatic serialization and pluginId.
    /// </summary>
    public void EmitEvent(string eventType, object payload, Dictionary<string, string>? metadata = null)
    {
        var @event = new PluginEvent
        {
            PluginId = _pluginId,
            Type = eventType,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = metadata
        };
        EmitEvent(@event);
    }

    public Task<string?> GetConfigAsync(string key, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(_storagePath, "config.json");
        
        if (!File.Exists(configPath))
        {
            return Task.FromResult<string?>(null);
        }

        var json = File.ReadAllText(configPath);
        var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        
        return Task.FromResult(config?.GetValueOrDefault(key));
    }

    public async Task SetConfigAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(_storagePath, "config.json");
        
        Dictionary<string, string> config;
        if (File.Exists(configPath))
        {
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        else
        {
            config = new Dictionary<string, string>();
        }

        config[key] = value;
        
        var newJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(configPath, newJson, cancellationToken);
    }

    public void ClearEvents()
    {
        _events.Clear();
    }
}
