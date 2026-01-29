using System.Text.Json.Serialization;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Base class for filter AST nodes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HasFilterNode), "HAS")]
[JsonDerivedType(typeof(HasAnyFilterNode), "HAS_ANY")]
[JsonDerivedType(typeof(HasAllFilterNode), "HAS_ALL")]
[JsonDerivedType(typeof(HasNoneFilterNode), "HAS_NONE")]
[JsonDerivedType(typeof(OrderStrictFilterNode), "ORDER_STRICT")]
[JsonDerivedType(typeof(OrderFlexFilterNode), "ORDER_FLEX")]
[JsonDerivedType(typeof(AndFilterNode), "AND")]
[JsonDerivedType(typeof(OrFilterNode), "OR")]
[JsonDerivedType(typeof(NotFilterNode), "NOT")]
[JsonDerivedType(typeof(ComponentTypeFilterNode), "COMPONENT_TYPE")]
[JsonDerivedType(typeof(ManagedFilterNode), "MANAGED")]
[JsonDerivedType(typeof(PublisherFilterNode), "PUBLISHER")]
[JsonDerivedType(typeof(AttributeFilterNode), "ATTRIBUTE")]
[JsonDerivedType(typeof(SolutionQueryNode), "SOLUTION_QUERY")]
public abstract class FilterNode
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
/// Represents a dynamic solution query for use in ORDER nodes.
/// Allows filtering solutions based on their schema name or other properties.
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
