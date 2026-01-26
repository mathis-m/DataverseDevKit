namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a Dataverse component.
/// </summary>
public sealed class Component
{
    /// <summary>
    /// Gets or sets the component ID (primary key).
    /// </summary>
    public Guid ComponentId { get; set; }

    /// <summary>
    /// Gets or sets the component type (e.g., Entity, Attribute, Form, View).
    /// </summary>
    public string ComponentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component type code.
    /// </summary>
    public int ComponentTypeCode { get; set; }

    /// <summary>
    /// Gets or sets the object ID.
    /// </summary>
    public Guid ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the logical name.
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the table logical name (for attributes, forms, views, etc.).
    /// </summary>
    public string? TableLogicalName { get; set; }

    /// <summary>
    /// Gets or sets additional metadata as JSON.
    /// </summary>
    public string? AdditionalMetadata { get; set; }

    /// <summary>
    /// Navigation property to layers.
    /// </summary>
    public List<Layer> Layers { get; set; } = new();
}
