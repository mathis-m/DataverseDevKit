using System.Text.Json.Serialization;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Base class for filter AST nodes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
// Component-level filters (top level)
[JsonDerivedType(typeof(AttributeFilterNode), "ATTRIBUTE")]
[JsonDerivedType(typeof(ComponentTypeFilterNode), "COMPONENT_TYPE")]
[JsonDerivedType(typeof(ManagedFilterNode), "MANAGED")]
[JsonDerivedType(typeof(PublisherFilterNode), "PUBLISHER")]
// Nested query filters
[JsonDerivedType(typeof(LayerQueryFilterNode), "LAYER_QUERY")]
[JsonDerivedType(typeof(SolutionQueryFilterNode), "SOLUTION_QUERY")]
[JsonDerivedType(typeof(LayerAttributeFilterNode), "LAYER_ATTRIBUTE")]
[JsonDerivedType(typeof(LayerAttributeQueryFilterNode), "LAYER_ATTRIBUTE_QUERY")]
// Layer attribute predicates (used inside LAYER_ATTRIBUTE_QUERY)
[JsonDerivedType(typeof(HasRelevantChangesFilterNode), "HAS_RELEVANT_CHANGES")]
[JsonDerivedType(typeof(HasAttributeDiffFilterNode), "HAS_ATTRIBUTE_DIFF")]
// Logical operators
[JsonDerivedType(typeof(AndFilterNode), "AND")]
[JsonDerivedType(typeof(OrFilterNode), "OR")]
[JsonDerivedType(typeof(NotFilterNode), "NOT")]
// Legacy filters (for backward compatibility - map to LayerQuery internally)
[JsonDerivedType(typeof(HasFilterNode), "HAS")]
[JsonDerivedType(typeof(HasAnyFilterNode), "HAS_ANY")]
[JsonDerivedType(typeof(HasAllFilterNode), "HAS_ALL")]
[JsonDerivedType(typeof(HasNoneFilterNode), "HAS_NONE")]
[JsonDerivedType(typeof(OrderStrictFilterNode), "ORDER_STRICT")]
[JsonDerivedType(typeof(OrderFlexFilterNode), "ORDER_FLEX")]
public abstract class FilterNode
{
}

/// <summary>
/// Base class for layer filter nodes (used within LayerQuery).
/// </summary>
public abstract class LayerFilterNode
{
}

/// <summary>
/// String comparison operators for attribute filters.
/// </summary>
public enum StringOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    BeginsWith,
    NotBeginsWith,
    EndsWith,
    NotEndsWith
}

/// <summary>
/// Attribute targets for filtering.
/// </summary>
public enum AttributeTarget
{
    LogicalName,
    DisplayName,
    ComponentType,
    Publisher,
    TableLogicalName
}

/// <summary>
/// Filter that checks if a component has a specific solution layer.
/// </summary>
public sealed class HasFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the solution name.
    /// </summary>
    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;
}

/// <summary>
/// Filter that checks if a component has any of the specified solution layers.
/// </summary>
public sealed class HasAnyFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the solution names.
    /// </summary>
    [JsonPropertyName("solutions")]
    public List<string> Solutions { get; set; } = new();
}

/// <summary>
/// Filter that checks if a component has all of the specified solution layers.
/// </summary>
public sealed class HasAllFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the solution names.
    /// </summary>
    [JsonPropertyName("solutions")]
    public List<string> Solutions { get; set; } = new();
}

/// <summary>
/// Filter that checks if a component has none of the specified solution layers.
/// </summary>
public sealed class HasNoneFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the solution names.
    /// </summary>
    [JsonPropertyName("solutions")]
    public List<string> Solutions { get; set; } = new();
}

/// <summary>
/// Filter that checks if layers appear in strict order (no other layers between them).
/// </summary>
public sealed class OrderStrictFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the sequence of solution names, choice groups, or solution queries.
    /// Each item can be:
    /// - A string (static solution name)
    /// - A list of strings (any of these solutions)
    /// - A SolutionQueryNode (dynamic solution selection)
    /// </summary>
    [JsonPropertyName("sequence")]
    public List<object> Sequence { get; set; } = new();
}

/// <summary>
/// Filter that checks if layers appear in flexible order (other layers may appear between them).
/// </summary>
public sealed class OrderFlexFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the sequence of solution names, choice groups, or solution queries.
    /// Each item can be:
    /// - A string (static solution name)
    /// - A list of strings (any of these solutions)
    /// - A SolutionQueryNode (dynamic solution selection)
    /// </summary>
    [JsonPropertyName("sequence")]
    public List<object> Sequence { get; set; } = new();
}

/// <summary>
/// Logical AND filter.
/// </summary>
public sealed class AndFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the child filters.
    /// </summary>
    [JsonPropertyName("children")]
    public List<FilterNode> Children { get; set; } = new();
}

/// <summary>
/// Logical OR filter.
/// </summary>
public sealed class OrFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the child filters.
    /// </summary>
    [JsonPropertyName("children")]
    public List<FilterNode> Children { get; set; } = new();
}

/// <summary>
/// Logical NOT filter.
/// </summary>
public sealed class NotFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the child filter.
    /// </summary>
    [JsonPropertyName("child")]
    public FilterNode? Child { get; set; }
}

/// <summary>
/// Filter by component type.
/// </summary>
public sealed class ComponentTypeFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the component type.
    /// </summary>
    [JsonPropertyName("componentType")]
    public string ComponentType { get; set; } = string.Empty;
}

/// <summary>
/// Filter by managed status.
/// </summary>
public sealed class ManagedFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets whether to filter for managed (true) or unmanaged (false) components.
    /// </summary>
    [JsonPropertyName("isManaged")]
    public bool IsManaged { get; set; }
}

/// <summary>
/// Filter by publisher.
/// </summary>
public sealed class PublisherFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the publisher name.
    /// </summary>
    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;
}

/// <summary>
/// Attribute-based filter with string operators.
/// Operates on component-level attributes.
/// </summary>
public sealed class AttributeFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the attribute target to filter on.
    /// </summary>
    [JsonPropertyName("attribute")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttributeTarget Attribute { get; set; }

    /// <summary>
    /// Gets or sets the string comparison operator.
    /// </summary>
    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StringOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Layer query filter - wraps filters that operate on component layers.
/// This is the primary way to filter based on solutions and layer ordering.
/// </summary>
public sealed class LayerQueryFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the layer filter to apply.
    /// Can be HAS, HAS_ANY, ORDER_FLEX, etc.
    /// </summary>
    [JsonPropertyName("layerFilter")]
    public FilterNode? LayerFilter { get; set; }
}

/// <summary>
/// Solution query filter - wraps filters that match solutions dynamically.
/// Used within layer queries to select solutions based on attributes.
/// </summary>
public sealed class SolutionQueryFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the attribute to filter on (typically SchemaName, UniqueName).
    /// </summary>
    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = "SchemaName";

    /// <summary>
    /// Gets or sets the string comparison operator.
    /// </summary>
    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StringOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Layer attribute filter - filters layers based on extracted attributes.
/// Used within LAYER_QUERY to filter by layer-specific attributes like CreatedOn, Publisher, etc.
/// </summary>
public sealed class LayerAttributeFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the attribute name to filter on (e.g., "formxml", "displayname", "createdon").
    /// </summary>
    [JsonPropertyName("attributeName")]
    public string AttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string comparison operator.
    /// </summary>
    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StringOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected attribute type (optional - for type-specific filtering).
    /// </summary>
    [JsonPropertyName("attributeType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Models.LayerAttributeType? AttributeType { get; set; }
}

/// <summary>
/// Filter that scopes layer attribute queries to a specific solution layer.
/// Used within LAYER_QUERY to filter by attributes of a specific solution's layer.
/// </summary>
public sealed class LayerAttributeQueryFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the target solution name to query attributes from.
    /// </summary>
    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the nested filter to apply to the layer's attributes.
    /// Can be AND, OR, NOT, or leaf predicates like HAS_RELEVANT_CHANGES.
    /// </summary>
    [JsonPropertyName("attributeFilter")]
    public FilterNode? AttributeFilter { get; set; }
}

/// <summary>
/// Predicate that checks if a layer has relevant (non-system) changes.
/// Evaluates to true if the layer has any changed attributes that are not in the excluded list.
/// </summary>
public sealed class HasRelevantChangesFilterNode : FilterNode
{
}

/// <summary>
/// Specifies how target solutions are determined for attribute diff comparison.
/// </summary>
public enum AttributeDiffTargetMode
{
    /// <summary>
    /// Compare against explicitly specified target solutions.
    /// </summary>
    Specific,

    /// <summary>
    /// Compare against all solution layers with lower ordinal (below the source layer).
    /// </summary>
    AllBelow
}

/// <summary>
/// Specifies how multiple attribute names are matched when checking for differences.
/// </summary>
public enum AttributeMatchLogic
{
    /// <summary>
    /// Diff is detected if ANY of the specified attributes differ.
    /// </summary>
    Any,

    /// <summary>
    /// Diff is detected only if ALL of the specified attributes differ.
    /// </summary>
    All
}

/// <summary>
/// Predicate that checks if attributes differ between a source solution layer and target solution layers.
/// Used to detect actual changes introduced by a solution layer.
/// </summary>
public sealed class HasAttributeDiffFilterNode : FilterNode
{
    /// <summary>
    /// Gets or sets the source solution name to compare from.
    /// </summary>
    [JsonPropertyName("sourceSolution")]
    public string SourceSolution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how target solutions are determined.
    /// </summary>
    [JsonPropertyName("targetMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttributeDiffTargetMode TargetMode { get; set; } = AttributeDiffTargetMode.AllBelow;

    /// <summary>
    /// Gets or sets the target solution names when TargetMode is Specific.
    /// Diff is true if source differs from ANY of these targets.
    /// </summary>
    [JsonPropertyName("targetSolutions")]
    public List<string>? TargetSolutions { get; set; }

    /// <summary>
    /// Gets or sets whether to only check attributes marked as changed in the source layer.
    /// </summary>
    [JsonPropertyName("onlyChangedAttributes")]
    public bool OnlyChangedAttributes { get; set; } = true;

    /// <summary>
    /// Gets or sets specific attribute names to check. Null means check any attribute.
    /// </summary>
    [JsonPropertyName("attributeNames")]
    public List<string>? AttributeNames { get; set; }

    /// <summary>
    /// Gets or sets the matching logic when multiple attribute names are specified.
    /// </summary>
    [JsonPropertyName("attributeMatchLogic")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttributeMatchLogic AttributeMatchLogic { get; set; } = AttributeMatchLogic.Any;
}

/// <summary>
/// Legacy SolutionQueryNode for backward compatibility.
/// Used in ORDER node sequences. Consider migrating to SolutionQueryFilterNode.
/// </summary>
public sealed class SolutionQueryNode
{
    /// <summary>
    /// Gets or sets the attribute to filter on (typically SchemaName).
    /// </summary>
    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = "SchemaName";

    /// <summary>
    /// Gets or sets the string comparison operator.
    /// </summary>
    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StringOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
