using DataverseDevKit.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Core.Abstractions;

/// <summary>
/// Provides context and services to a plugin during execution.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Gets the logger for this plugin.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Gets the storage path for this plugin's isolated data.
    /// </summary>
    string StoragePath { get; }

    /// <summary>
    /// Emits an event from this plugin to the host/UI.
    /// </summary>
    /// <param name="pluginEvent">The event to emit.</param>
    void EmitEvent(PluginEvent pluginEvent);

    /// <summary>
    /// Gets a configuration value for this plugin.
    /// </summary>
    Task<string?> GetConfigAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value for this plugin.
    /// </summary>
    Task SetConfigAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a Dataverse client for the specified connection.
    /// </summary>
    /// <param name="connectionId">The connection ID. If null, uses the active connection.</param>
    /// <returns>A Dataverse client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not found or not authenticated.</exception>
    IDataverseClient GetDataverseClient(string? connectionId = null);
}
