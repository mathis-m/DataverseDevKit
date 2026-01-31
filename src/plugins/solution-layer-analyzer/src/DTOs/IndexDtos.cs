namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the index command.
/// </summary>
public sealed record IndexRequest
{
    /// <summary>
    /// Gets the connection ID.
    /// </summary>
    public string ConnectionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source solution names.
    /// </summary>
    public List<string> SourceSolutions { get; init; } = new();

    /// <summary>
    /// Gets the target solution names.
    /// </summary>
    public List<string> TargetSolutions { get; init; } = new();

    /// <summary>
    /// Gets the component types to include.
    /// </summary>
    public List<string> IncludeComponentTypes { get; init; } = new();

    /// <summary>
    /// Gets the maximum parallel operations.
    /// </summary>
    public int MaxParallel { get; init; } = 8;

    /// <summary>
    /// Gets the payload loading mode ("lazy" or "eager").
    /// </summary>
    public string PayloadMode { get; init; } = "lazy";
}

/// <summary>
/// Response for the index command (async operation started).
/// </summary>
public sealed record IndexResponse
{
    /// <summary>
    /// Gets the operation ID to track the indexing operation.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was started successfully.
    /// </summary>
    public bool Started { get; init; }

    /// <summary>
    /// Gets an error message if the operation failed to start.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Completion event payload for index operation.
/// </summary>
public sealed record IndexCompletionEvent
{
    /// <summary>
    /// Gets the operation ID.
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the statistics if successful.
    /// </summary>
    public IndexStats? Stats { get; init; }

    /// <summary>
    /// Gets the warnings.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Gets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Statistics from indexing.
/// </summary>
public sealed record IndexStats
{
    /// <summary>
    /// Gets the number of solutions indexed.
    /// </summary>
    public int Solutions { get; init; }

    /// <summary>
    /// Gets the number of components indexed.
    /// </summary>
    public int Components { get; init; }

    /// <summary>
    /// Gets the number of layers indexed.
    /// </summary>
    public int Layers { get; init; }
}

/// <summary>
/// Request for the getIndexMetadata command.
/// </summary>
public sealed record IndexMetadataRequest
{
    /// <summary>
    /// Gets the connection ID.
    /// </summary>
    public string ConnectionId { get; init; } = string.Empty;
}

/// <summary>
/// Response for the getIndexMetadata command.
/// </summary>
public sealed record IndexMetadataResponse
{
    /// <summary>
    /// Gets a value indicating whether an index exists.
    /// </summary>
    public bool HasIndex { get; init; }

    /// <summary>
    /// Gets the source solution names used to build the index.
    /// </summary>
    public List<string> SourceSolutions { get; init; } = new();

    /// <summary>
    /// Gets the target solution names used to build the index.
    /// </summary>
    public List<string> TargetSolutions { get; init; } = new();

    /// <summary>
    /// Gets the statistics if an index exists.
    /// </summary>
    public IndexStats? Stats { get; init; }
}
