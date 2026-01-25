namespace DataverseDevKit.Core.Models;

/// <summary>
/// Represents an event emitted by a plugin to the host/UI.
/// </summary>
public record PluginEvent
{
    /// <summary>
    /// Gets the event type/topic.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the event payload (JSON serialized).
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Gets the timestamp when the event was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets optional metadata for the event.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
