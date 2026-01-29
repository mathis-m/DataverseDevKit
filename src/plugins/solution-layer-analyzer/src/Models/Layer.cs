namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a solution layer for a component.
/// </summary>
public sealed class Layer
{
    /// <summary>
    /// Gets or sets the layer ID (primary key).
    /// </summary>
    public Guid LayerId { get; set; }

    /// <summary>
    /// Gets or sets the component ID (foreign key).
    /// </summary>
    public Guid ComponentId { get; set; }

    /// <summary>
    /// Gets or sets the ordinal position (0 = base, higher = top).
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Gets or sets the solution ID.
    /// </summary>
    public Guid SolutionId { get; set; }

    /// <summary>
    /// Gets or sets the solution name.
    /// </summary>
    public string SolutionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the publisher.
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this layer is managed.
    /// </summary>
    public bool IsManaged { get; set; }

    /// <summary>
    /// Gets or sets the solution version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the layer was created.
    /// </summary>
    public DateTimeOffset? CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the component JSON from msdyn_componentjson field.
    /// Contains the full entity attributes for this layer.
    /// </summary>
    public string? ComponentJson { get; set; }

    /// <summary>
    /// Navigation property to component.
    /// </summary>
    public Component Component { get; set; } = null!;

    /// <summary>
    /// Navigation property to extracted layer attributes.
    /// </summary>
    public List<LayerAttribute> Attributes { get; set; } = new();
}
