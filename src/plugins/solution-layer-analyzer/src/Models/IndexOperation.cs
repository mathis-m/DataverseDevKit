namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Tracks the status of a long-running index operation.
/// </summary>
public sealed class IndexOperation
{
    /// <summary>
    /// Gets or sets the operation ID.
    /// </summary>
    public Guid OperationId { get; set; }

    /// <summary>
    /// Gets or sets the operation status.
    /// </summary>
    public IndexOperationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the operation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the operation completed (success or failure).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the statistics if successful.
    /// </summary>
    public string? StatsJson { get; set; }

    /// <summary>
    /// Gets or sets warnings if any.
    /// </summary>
    public string? WarningsJson { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of an index operation.
/// </summary>
public enum IndexOperationStatus
{
    /// <summary>
    /// Operation is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Operation failed with an error.
    /// </summary>
    Failed
}
