namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents an extracted attribute from a layer's component JSON.
/// Attributes are extracted during indexing and stored in a queryable format.
/// </summary>
public sealed class LayerAttribute
{
    /// <summary>
    /// Gets or sets the attribute ID (primary key).
    /// </summary>
    public Guid AttributeId { get; set; }

    /// <summary>
    /// Gets or sets the layer ID (foreign key).
    /// </summary>
    public Guid LayerId { get; set; }

    /// <summary>
    /// Gets or sets the attribute name (e.g., "formxml", "fetchxml", "displayname").
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pre-formatted attribute value.
    /// Complex values (JSON, XML) are normalized and formatted during indexing.
    /// </summary>
    public string? AttributeValue { get; set; }

    /// <summary>
    /// Gets or sets the attribute type for type-safe querying.
    /// </summary>
    public LayerAttributeType AttributeType { get; set; }

    /// <summary>
    /// Gets or sets whether this attribute contained a complex value (JSON/XML).
    /// Complex values are pre-formatted for display.
    /// </summary>
    public bool IsComplexValue { get; set; }

    /// <summary>
    /// Gets or sets the raw value for attributes that need both formatted and raw.
    /// </summary>
    public string? RawValue { get; set; }

    /// <summary>
    /// Gets or sets whether this attribute was changed in this layer.
    /// Based on presence in msdyn_changes Attributes array.
    /// </summary>
    public bool IsChanged { get; set; }

    /// <summary>
    /// Navigation property to layer.
    /// </summary>
    public Layer Layer { get; set; } = null!;
}

/// <summary>
/// Defines the type of a layer attribute for type-safe querying.
/// </summary>
public enum LayerAttributeType
{
    /// <summary>
    /// Simple string value.
    /// </summary>
    String,

    /// <summary>
    /// Numeric value (integer or decimal).
    /// </summary>
    Number,

    /// <summary>
    /// Boolean value.
    /// </summary>
    Boolean,

    /// <summary>
    /// DateTime value.
    /// </summary>
    DateTime,

    /// <summary>
    /// JSON value (object or array, pre-formatted).
    /// </summary>
    Json,

    /// <summary>
    /// XML value (pre-formatted).
    /// </summary>
    Xml,

    /// <summary>
    /// Entity reference (contains name and ID).
    /// </summary>
    EntityReference,

    /// <summary>
    /// OptionSet value (contains value and label).
    /// </summary>
    OptionSet,

    /// <summary>
    /// Money value.
    /// </summary>
    Money,

    /// <summary>
    /// Lookup reference.
    /// </summary>
    Lookup
}
