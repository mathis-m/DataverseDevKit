namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the diff command.
/// </summary>
public sealed record DiffRequest
{
    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public Guid ComponentId { get; init; }

    /// <summary>
    /// Gets the connection ID.
    /// </summary>
    public string? ConnectionId { get; init; }

    /// <summary>
    /// Gets the left layer.
    /// </summary>
    public DiffLayer Left { get; init; } = new();

    /// <summary>
    /// Gets the right layer.
    /// </summary>
    public DiffLayer Right { get; init; } = new();
}

/// <summary>
/// Diff layer specification.
/// </summary>
public sealed record DiffLayer
{
    /// <summary>
    /// Gets the solution name.
    /// </summary>
    public string SolutionName { get; init; } = string.Empty;
}

/// <summary>
/// Response for the diff command.
/// </summary>
public sealed record DiffResponse
{
    /// <summary>
    /// Gets the list of attribute differences.
    /// </summary>
    public List<AttributeDiff> Attributes { get; init; } = new();

    /// <summary>
    /// Gets any warnings.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Represents a difference in an attribute between two layers.
/// </summary>
public sealed record AttributeDiff
{
    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    public string AttributeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted value from the left layer.
    /// </summary>
    public string? LeftValue { get; init; }

    /// <summary>
    /// Gets the formatted value from the right layer.
    /// </summary>
    public string? RightValue { get; init; }

    /// <summary>
    /// Gets the attribute type.
    /// </summary>
    public int AttributeType { get; init; }

    /// <summary>
    /// Gets whether this is a complex value (JSON/XML).
    /// </summary>
    public bool IsComplex { get; init; }

    /// <summary>
    /// Gets whether the attribute only exists in the left layer.
    /// </summary>
    public bool OnlyInLeft { get; init; }

    /// <summary>
    /// Gets whether the attribute only exists in the right layer.
    /// </summary>
    public bool OnlyInRight { get; init; }

    /// <summary>
    /// Gets whether the values are different.
    /// </summary>
    public bool IsDifferent { get; init; }
}
