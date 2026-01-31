using System.Linq.Expressions;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Represents a query execution plan that combines SQL and in-memory filtering.
/// </summary>
public sealed class QueryPlan
{
    /// <summary>
    /// The portion of the filter that can be executed as SQL.
    /// Applied via EF Core Where() clause.
    /// </summary>
    public Expression<Func<Component, bool>>? SqlExpression { get; init; }

    /// <summary>
    /// The original filter for in-memory evaluation of portions that couldn't be translated.
    /// Null if the entire filter was translated to SQL.
    /// </summary>
    public FilterNode? InMemoryFilter { get; init; }

    /// <summary>
    /// Pre-fetch operations needed before building the final SQL expression.
    /// </summary>
    public List<PreFetchOperation> PreFetchOperations { get; init; } = new();

    /// <summary>
    /// Whether the entire filter was translated to SQL.
    /// </summary>
    public bool IsFullySqlTranslatable => InMemoryFilter == null && PreFetchOperations.Count == 0;

    /// <summary>
    /// Whether the plan requires pre-fetch operations.
    /// </summary>
    public bool RequiresPreFetch => PreFetchOperations.Count > 0;

    /// <summary>
    /// Description of the query plan for debugging/logging.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Classification of the filter.
    /// </summary>
    public FilterClassification? Classification { get; init; }
}

/// <summary>
/// Types of pre-fetch operations that can be executed before the main query.
/// </summary>
public enum PreFetchType
{
    /// <summary>
    /// Fetch ordinal ranges for solutions (used by ORDER filters).
    /// </summary>
    OrdinalRanges,

    /// <summary>
    /// Resolve solution names matching a query (used by SolutionQueryNode).
    /// </summary>
    SolutionNameResolution,

    /// <summary>
    /// Fetch component IDs matching a subquery (used for complex EXISTS).
    /// </summary>
    ComponentIdSubquery
}

/// <summary>
/// Defines a pre-fetch operation to execute before the main query.
/// </summary>
public sealed class PreFetchOperation
{
    /// <summary>
    /// The type of pre-fetch operation.
    /// </summary>
    public PreFetchType Type { get; init; }

    /// <summary>
    /// Solution names to fetch ordinals for (OrdinalRanges type).
    /// </summary>
    public List<string>? SolutionNames { get; init; }

    /// <summary>
    /// Solution query to resolve (SolutionNameResolution type).
    /// </summary>
    public SolutionQueryNode? SolutionQuery { get; init; }

    /// <summary>
    /// Key to store results under (for lookup in SqlFilterBuilder).
    /// </summary>
    public string ResultKey { get; init; } = string.Empty;

    /// <summary>
    /// Description of the operation for logging.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Results from pre-fetch operations.
/// </summary>
public sealed class PreFetchResults
{
    /// <summary>
    /// Ordinal ranges per solution name.
    /// Key: solution name (case-insensitive), Value: (minOrdinal, maxOrdinal)
    /// </summary>
    public Dictionary<string, (int Min, int Max)> OrdinalRanges { get; init; } = 
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolved solution names from queries.
    /// Key: query key, Value: list of matching solution names
    /// </summary>
    public Dictionary<string, List<string>> ResolvedSolutionNames { get; init; } = 
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Component IDs from subqueries.
    /// Key: subquery key, Value: set of matching component IDs
    /// </summary>
    public Dictionary<string, HashSet<Guid>> ComponentIdSets { get; init; } = new();
}

/// <summary>
/// Configuration options for query plan building.
/// </summary>
public sealed class QueryPlanOptions
{
    /// <summary>
    /// Maximum number of components to load per batch when using hybrid queries.
    /// </summary>
    public int MaxBatchSize { get; init; } = 10000;

    /// <summary>
    /// Whether to force in-memory evaluation (for debugging/comparison).
    /// </summary>
    public bool ForceInMemory { get; init; }

    /// <summary>
    /// Whether to log generated SQL for debugging.
    /// </summary>
    public bool LogGeneratedSql { get; init; }

    /// <summary>
    /// Timeout for pre-fetch operations in milliseconds.
    /// </summary>
    public int PreFetchTimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Statistics about query plan execution.
/// </summary>
public sealed class QueryPlanStats
{
    /// <summary>
    /// Time spent on pre-fetch operations.
    /// </summary>
    public TimeSpan PreFetchDuration { get; set; }

    /// <summary>
    /// Time spent on SQL query execution.
    /// </summary>
    public TimeSpan SqlQueryDuration { get; set; }

    /// <summary>
    /// Time spent on in-memory filtering.
    /// </summary>
    public TimeSpan InMemoryFilterDuration { get; set; }

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Number of rows returned by SQL query (before in-memory filter).
    /// </summary>
    public int RowsFromSql { get; set; }

    /// <summary>
    /// Number of rows after in-memory filtering.
    /// </summary>
    public int RowsAfterFilter { get; set; }

    /// <summary>
    /// Filter efficiency ratio (RowsAfterFilter / RowsFromSql).
    /// Higher is better - means SQL filter was effective.
    /// </summary>
    public double FilterEfficiency => RowsFromSql > 0 
        ? (double)RowsAfterFilter / RowsFromSql 
        : 1.0;

    /// <summary>
    /// Whether the query used in-memory filtering.
    /// </summary>
    public bool UsedInMemoryFilter { get; set; }

    /// <summary>
    /// Description of the query plan used.
    /// </summary>
    public string PlanDescription { get; set; } = string.Empty;
}
