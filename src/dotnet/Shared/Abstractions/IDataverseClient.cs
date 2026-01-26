namespace DataverseDevKit.Core.Abstractions;

/// <summary>
/// Provides Dataverse query and execute capabilities to plugins.
/// </summary>
public interface IDataverseClient
{
    /// <summary>
    /// Gets the connection ID for the current context.
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Queries Dataverse using Web API OData query.
    /// </summary>
    /// <param name="entityName">The logical name of the entity (e.g., "solution", "systemform").</param>
    /// <param name="query">OData query string (e.g., "&lt;select=name&amp;&lt;filter=statecode eq 0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON array of results.</returns>
    Task<DataverseQueryResult> QueryAsync(string entityName, string? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single record by ID.
    /// </summary>
    /// <param name="entityName">The logical name of the entity.</param>
    /// <param name="id">The record ID.</param>
    /// <param name="selectClause">Optional select clause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object representing the record.</returns>
    Task<DataverseQueryResult> RetrieveAsync(string entityName, Guid id, string? selectClause = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a Dataverse request (e.g., custom actions, functions).
    /// </summary>
    /// <param name="requestJson">The request as JSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response as JSON.</returns>
    Task<DataverseQueryResult> ExecuteAsync(string requestJson, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a Dataverse query operation.
/// </summary>
public record DataverseQueryResult
{
    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the result data as JSON string.
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the number of records returned (for queries).
    /// </summary>
    public int RecordCount { get; init; }
}
