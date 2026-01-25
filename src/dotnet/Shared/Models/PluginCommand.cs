namespace DataverseDevKit.Core.Models;

/// <summary>
/// Represents a command exposed by a plugin.
/// </summary>
public record PluginCommand
{
    /// <summary>
    /// Gets the command name (unique within the plugin).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the command display label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the command description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the JSON schema for the command payload (optional).
    /// </summary>
    public string? PayloadSchema { get; init; }
}
