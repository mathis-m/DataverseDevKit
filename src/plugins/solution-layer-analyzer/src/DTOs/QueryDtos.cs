namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the query command.
/// </summary>
public sealed record QueryRequest
{
    /// <summary>
    /// Gets the filters (AST).
    /// </summary>
    public object? Filters { get; init; }

    /// <summary>
    /// Gets the group by fields.
    /// </summary>
    public List<string> GroupBy { get; init; } = new();

    /// <summary>
    /// Gets the fields to select.
    /// </summary>
    public List<string> Select { get; init; } = new();

    /// <summary>
    /// Gets the paging settings.
    /// </summary>
    public PagingSettings Paging { get; init; } = new();

    /// <summary>
    /// Gets the sort settings.
    /// </summary>
    public List<SortSettings> Sort { get; init; } = new();
}

/// <summary>
/// Paging settings.
/// </summary>
public sealed record PagingSettings
{
    /// <summary>
    /// Gets the number of items to skip.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Gets the number of items to take.
    /// </summary>
    public int Take { get; init; } = 500;
}

/// <summary>
/// Sort settings.
/// </summary>
public sealed record SortSettings
{
    /// <summary>
    /// Gets the field name.
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Gets the direction ("asc" or "desc").
    /// </summary>
    public string Dir { get; init; } = "asc";
}

/// <summary>
/// Response for the query command.
/// </summary>
public sealed record QueryResponse
{
    /// <summary>
    /// Gets the result rows.
    /// </summary>
    public List<ComponentResult> Rows { get; init; } = new();

    /// <summary>
    /// Gets the total count.
    /// </summary>
    public int Total { get; init; }
}

/// <summary>
/// Component result.
/// </summary>
public sealed record ComponentResult
{
    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public Guid ComponentId { get; init; }

    /// <summary>
    /// Gets the component type.
    /// </summary>
    public string ComponentType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the logical name.
    /// </summary>
    public string LogicalName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the layer sequence (solution names in order).
    /// </summary>
    public List<string> LayerSequence { get; init; } = new();

    /// <summary>
    /// Gets whether this component is managed.
    /// </summary>
    public bool IsManaged { get; init; }

    /// <summary>
    /// Gets the publisher.
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Gets the table logical name.
    /// </summary>
    public string? TableLogicalName { get; init; }
}
