namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a cached component payload (for diff operations).
/// </summary>
public sealed class Artifact
{
    /// <summary>
    /// Gets or sets the artifact ID (primary key).
    /// </summary>
    public Guid ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the component ID.
    /// </summary>
    public Guid ComponentId { get; set; }

    /// <summary>
    /// Gets or sets the solution ID.
    /// </summary>
    public Guid SolutionId { get; set; }

    /// <summary>
    /// Gets or sets the payload type (xml, json, text).
    /// </summary>
    public string PayloadType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload content.
    /// </summary>
    public string PayloadText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the artifact was cached.
    /// </summary>
    public DateTimeOffset CachedOn { get; set; } = DateTimeOffset.UtcNow;
}
