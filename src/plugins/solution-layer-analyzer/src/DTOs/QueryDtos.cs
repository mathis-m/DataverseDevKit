using Ddk.SolutionLayerAnalyzer.Filters;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the query command.
/// </summary>
public sealed record QueryRequest
{
    /// <summary>
    /// Gets the unique query ID for correlating requests with responses.
    /// Used by the frontend to ignore stale results from superseded queries.
    /// </summary>
    public string? QueryId { get; init; }

    /// <summary>
    /// Gets the connection ID for identifying the database.
    /// </summary>
    public string ConnectionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the filters (AST).
    /// </summary>
    public FilterNode? Filters { get; init; }

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

    /// <summary>
    /// Gets the query plan options (optional).
    /// </summary>
    public QueryPlanOptions? PlanOptions { get; init; }

    /// <summary>
    /// When true, the query result will be emitted as an event instead of returned directly.
    /// The frontend should listen for 'plugin:sla:query-result' events.
    /// </summary>
    public bool UseEventResponse { get; init; }
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
    /// Gets the query ID from the request (for correlation).
    /// </summary>
    public string? QueryId { get; init; }

    /// <summary>
    /// Gets the result rows.
    /// </summary>
    public List<ComponentResult> Rows { get; init; } = new();

    /// <summary>
    /// Gets the total count.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the query execution statistics (optional, for diagnostics).
    /// </summary>
    public QueryPlanStats? Stats { get; init; }
}

/// <summary>
/// Event payload for query results when using event-based responses.
/// </summary>
public sealed record QueryResultEvent
{
    /// <summary>
    /// Gets the query ID for correlation.
    /// </summary>
    public string QueryId { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the query succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the result rows (when successful).
    /// </summary>
    public List<ComponentResult>? Rows { get; init; }

    /// <summary>
    /// Gets the total count (when successful).
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the query execution statistics (optional).
    /// </summary>
    public QueryPlanStats? Stats { get; init; }

    /// <summary>
    /// Gets the error message (when failed).
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Acknowledgment response when using event-based queries.
/// Returned immediately to confirm the query was started.
/// </summary>
public sealed record QueryAcknowledgment
{
    /// <summary>
    /// Gets the query ID.
    /// </summary>
    public string QueryId { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the query was started.
    /// </summary>
    public bool Started { get; init; }

    /// <summary>
    /// Gets any immediate error message (e.g., invalid request).
    /// </summary>
    public string? ErrorMessage { get; init; }
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
