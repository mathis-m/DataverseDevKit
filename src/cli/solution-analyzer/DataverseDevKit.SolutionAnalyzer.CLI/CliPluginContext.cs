using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;
using Ddk.SolutionLayerAnalyzer.DTOs;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Simple plugin context implementation for CLI usage.
/// Captures events for polling by the CLI.
/// </summary>
internal class CliPluginContext : IPluginContext
{
    private readonly ILogger _logger;
    private readonly string _storagePath;
    private readonly IServiceClientFactory _serviceClientFactory;

    // Event storage for CLI polling
    private readonly ConcurrentDictionary<string, ReportProgressEvent> _progressEvents = new();
    private readonly ConcurrentDictionary<string, ReportCompletionEvent> _completionEvents = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        // Log events
        _logger.LogDebug("Plugin Event: {EventType} - {Payload}", 
            pluginEvent.Type, pluginEvent.Payload);

        // Store events for CLI polling
        if (pluginEvent.Type == "plugin:sla:report-progress")
        {
            try
            {
                var progressEvent = JsonSerializer.Deserialize<ReportProgressEvent>(pluginEvent.Payload, JsonOptions);
                if (progressEvent != null)
                {
                    _progressEvents[progressEvent.OperationId] = progressEvent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse progress event");
            }
        }
        else if (pluginEvent.Type == "plugin:sla:report-complete")
        {
            try
            {
                var completionEvent = JsonSerializer.Deserialize<ReportCompletionEvent>(pluginEvent.Payload, JsonOptions);
                if (completionEvent != null)
                {
                    _completionEvents[completionEvent.OperationId] = completionEvent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse completion event");
            }
        }
    }

    /// <summary>
    /// Try to get the latest progress event for an operation.
    /// </summary>
    public bool TryGetProgressEvent(string operationId, out ReportProgressEvent progressEvent)
    {
        return _progressEvents.TryGetValue(operationId, out progressEvent!);
    }

    /// <summary>
    /// Try to get the completion event for an operation.
    /// </summary>
    public bool TryGetCompletionEvent(string operationId, out ReportCompletionEvent completionEvent)
    {
        return _completionEvents.TryGetValue(operationId, out completionEvent!);
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
