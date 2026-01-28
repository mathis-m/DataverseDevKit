namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a cached component name entry.
/// Used to cache resolved display names and logical names for components.
/// </summary>
public sealed class ComponentNameCache
{
    /// <summary>
    /// Gets or sets the unique identifier for this cache entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the object ID of the component.
    /// </summary>
    public Guid ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the component type code.
    /// </summary>
    public int ComponentTypeCode { get; set; }

    /// <summary>
    /// Gets or sets the resolved logical name.
    /// Falls back to object ID if not resolvable.
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolved display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the parent table logical name (for entity-scoped components like attributes, forms, views).
    /// </summary>
    public string? TableLogicalName { get; set; }

    /// <summary>
    /// Gets or sets when this cache entry was created.
    /// </summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Gets or sets when this cache entry expires (optional, for future TTL support).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
