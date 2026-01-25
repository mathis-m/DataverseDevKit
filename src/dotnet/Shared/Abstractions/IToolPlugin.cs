using DataverseDevKit.Core.Models;

namespace DataverseDevKit.Core.Abstractions;

/// <summary>
/// Base interface for all DDK plugins.
/// </summary>
public interface IToolPlugin
{
    /// <summary>
    /// Gets the unique plugin ID (e.g., com.contoso.ddk.pluginname).
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Gets the plugin display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Initializes the plugin with the given context.
    /// </summary>
    /// <param name="context">The plugin execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of commands supported by this plugin.
    /// </summary>
    Task<IReadOnlyList<PluginCommand>> GetCommandsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command with the given payload.
    /// </summary>
    /// <param name="commandName">The command to execute.</param>
    /// <param name="payload">The command payload (JSON serialized).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Command result (JSON serialized).</returns>
    Task<string> ExecuteAsync(string commandName, string payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes the plugin and releases resources.
    /// </summary>
    Task DisposeAsync();
}
