using System.Text.Json;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Filters;

/// <summary>
/// Builds query execution plans from filter trees.
/// Analyzes filters to determine which parts can be translated to SQL
/// and which must be evaluated in memory.
/// </summary>
public sealed class QueryPlanBuilder
{
    private readonly FilterClassifier _classifier = new();
    private readonly QueryPlanOptions _options;

    /// <summary>
    /// Creates a new QueryPlanBuilder with default options.
    /// </summary>
    public QueryPlanBuilder() : this(new QueryPlanOptions())
    {
    }

    /// <summary>
    /// Creates a new QueryPlanBuilder with the specified options.
    /// </summary>
    public QueryPlanBuilder(QueryPlanOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Builds a query plan for the given filter.
    /// </summary>
    public QueryPlan BuildPlan(FilterNode? filter)
    {
        if (_options.ForceInMemory)
        {
            return new QueryPlan
            {
                SqlExpression = c => true,
                InMemoryFilter = filter,
                Description = "Forced in-memory evaluation",
                Classification = _classifier.Classify(filter)
            };
        }

        if (filter == null)
        {
            return new QueryPlan
            {
                SqlExpression = c => true,
                InMemoryFilter = null,
                Description = "No filter - return all components",
                Classification = _classifier.Classify(null)
            };
        }

        var classification = _classifier.Classify(filter);

        return classification.Capability switch
        {
            FilterCapability.FullySqlTranslatable => BuildFullySqlPlan(filter, classification),
            FilterCapability.HybridSqlTranslatable => BuildHybridPlan(filter, classification),
            FilterCapability.InMemoryOnly => BuildInMemoryPlan(filter, classification),
            _ => BuildInMemoryPlan(filter, classification)
        };
    }

    private QueryPlan BuildFullySqlPlan(FilterNode filter, FilterClassification classification)
    {
        var builder = new SqlFilterBuilder();
        var sqlExpr = builder.BuildExpression(filter);

        return new QueryPlan
        {
            SqlExpression = sqlExpr,
            InMemoryFilter = null,
            Description = $"Fully SQL translatable: {classification.Reason}",
            Classification = classification
        };
    }

    private QueryPlan BuildHybridPlan(FilterNode filter, FilterClassification classification)
    {
        var preFetchOps = CollectPreFetchOperations(filter);

        // For hybrid filters, we build a best-effort SQL expression
        // and keep the full filter for in-memory verification
        var builder = new SqlFilterBuilder();
        var sqlExpr = builder.BuildExpression(filter);

        // Determine if we need in-memory verification
        // ORDER filters always need in-memory for now (until we implement CTE-based ordinal checks)
        var needsInMemoryVerification = ContainsOrderFilter(filter);

        return new QueryPlan
        {
            SqlExpression = sqlExpr,
            InMemoryFilter = needsInMemoryVerification ? filter : null,
            PreFetchOperations = preFetchOps,
            Description = $"Hybrid plan with {preFetchOps.Count} pre-fetch ops: {classification.Reason}",
            Classification = classification
        };
    }

    private QueryPlan BuildInMemoryPlan(FilterNode filter, FilterClassification classification)
    {
        // Try to extract any SQL-translatable portions
        var (sqlPortion, inMemoryPortion) = SplitFilter(filter);

        if (sqlPortion != null)
        {
            var builder = new SqlFilterBuilder();
            var sqlExpr = builder.BuildExpression(sqlPortion);

            return new QueryPlan
            {
                SqlExpression = sqlExpr,
                InMemoryFilter = inMemoryPortion ?? filter,
                Description = $"Partial SQL translation: {classification.Reason}",
                Classification = classification
            };
        }

        return new QueryPlan
        {
            SqlExpression = c => true,
            InMemoryFilter = filter,
            Description = $"In-memory only: {classification.Reason}",
            Classification = classification
        };
    }

    /// <summary>
    /// Collects pre-fetch operations needed for hybrid filters.
    /// </summary>
    private List<PreFetchOperation> CollectPreFetchOperations(FilterNode filter)
    {
        var operations = new List<PreFetchOperation>();
        CollectPreFetchOperationsRecursive(filter, operations);
        return operations;
    }

    private void CollectPreFetchOperationsRecursive(FilterNode? filter, List<PreFetchOperation> operations)
    {
        if (filter == null) return;

        switch (filter)
        {
            case OrderStrictFilterNode orderStrict:
                AddOrderFilterPreFetch(orderStrict.Sequence, operations);
                break;

            case OrderFlexFilterNode orderFlex:
                AddOrderFilterPreFetch(orderFlex.Sequence, operations);
                break;

            case AndFilterNode and:
                foreach (var child in and.Children)
                    CollectPreFetchOperationsRecursive(child, operations);
                break;

            case OrFilterNode or:
                foreach (var child in or.Children)
                    CollectPreFetchOperationsRecursive(child, operations);
                break;

            case NotFilterNode not:
                CollectPreFetchOperationsRecursive(not.Child, operations);
                break;

            case LayerQueryFilterNode lq:
                CollectPreFetchOperationsRecursive(lq.LayerFilter, operations);
                break;

            case LayerAttributeQueryFilterNode laq:
                CollectPreFetchOperationsRecursive(laq.AttributeFilter, operations);
                break;
        }
    }

    private void AddOrderFilterPreFetch(List<object> sequence, List<PreFetchOperation> operations)
    {
        var solutionNames = new List<string>();
        var solutionQueries = new List<SolutionQueryNode>();

        foreach (var item in sequence)
        {
            if (item is string s)
            {
                solutionNames.Add(s);
            }
            else if (item is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in json.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            solutionNames.Add(element.GetString() ?? string.Empty);
                        }
                    }
                }
                else if (json.ValueKind == JsonValueKind.String)
                {
                    solutionNames.Add(json.GetString() ?? string.Empty);
                }
                else if (json.ValueKind == JsonValueKind.Object)
                {
                    try
                    {
                        var query = JsonSerializer.Deserialize<SolutionQueryNode>(json.GetRawText());
                        if (query != null)
                        {
                            solutionQueries.Add(query);
                        }
                    }
                    catch
                    {
                        // Not a valid query, skip
                    }
                }
            }
            else if (item is SolutionQueryNode query)
            {
                solutionQueries.Add(query);
            }
        }

        // Add ordinal range pre-fetch for known solution names
        if (solutionNames.Count > 0)
        {
            var distinctNames = solutionNames.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            if (distinctNames.Count > 0)
            {
                operations.Add(new PreFetchOperation
                {
                    Type = PreFetchType.OrdinalRanges,
                    SolutionNames = distinctNames,
                    ResultKey = "ordinals",
                    Description = $"Fetch ordinal ranges for {distinctNames.Count} solutions"
                });
            }
        }

        // Add solution name resolution pre-fetch for queries
        foreach (var query in solutionQueries)
        {
            var key = $"solution_query_{query.Operator}_{query.Value}";
            if (!operations.Any(o => o.ResultKey == key))
            {
                operations.Add(new PreFetchOperation
                {
                    Type = PreFetchType.SolutionNameResolution,
                    SolutionQuery = query,
                    ResultKey = key,
                    Description = $"Resolve solutions where {query.Attribute} {query.Operator} '{query.Value}'"
                });
            }
        }
    }

    /// <summary>
    /// Attempts to split a filter into SQL-translatable and in-memory portions.
    /// </summary>
    private (FilterNode? SqlPortion, FilterNode? InMemoryPortion) SplitFilter(FilterNode filter)
    {
        // For AND filters, we can split: SQL-translatable children become one filter,
        // in-memory children become another
        if (filter is AndFilterNode and)
        {
            var sqlChildren = new List<FilterNode>();
            var inMemoryChildren = new List<FilterNode>();

            foreach (var child in and.Children)
            {
                var childClass = _classifier.Classify(child);
                if (childClass.Capability == FilterCapability.FullySqlTranslatable)
                {
                    sqlChildren.Add(child);
                }
                else
                {
                    inMemoryChildren.Add(child);
                }
            }

            FilterNode? sqlPortion = sqlChildren.Count switch
            {
                0 => null,
                1 => sqlChildren[0],
                _ => new AndFilterNode { Children = sqlChildren }
            };

            FilterNode? inMemoryPortion = inMemoryChildren.Count switch
            {
                0 => null,
                1 => inMemoryChildren[0],
                _ => new AndFilterNode { Children = inMemoryChildren }
            };

            return (sqlPortion, inMemoryPortion);
        }

        // For other filter types, we can't easily split
        var classification = _classifier.Classify(filter);
        if (classification.Capability == FilterCapability.FullySqlTranslatable)
        {
            return (filter, null);
        }

        return (null, filter);
    }

    /// <summary>
    /// Checks if the filter tree contains any ORDER filters.
    /// </summary>
    private bool ContainsOrderFilter(FilterNode? filter)
    {
        if (filter == null) return false;

        return filter switch
        {
            OrderStrictFilterNode => true,
            OrderFlexFilterNode => true,
            AndFilterNode and => and.Children.Any(ContainsOrderFilter),
            OrFilterNode or => or.Children.Any(ContainsOrderFilter),
            NotFilterNode not => ContainsOrderFilter(not.Child),
            LayerQueryFilterNode lq => ContainsOrderFilter(lq.LayerFilter),
            LayerAttributeQueryFilterNode laq => ContainsOrderFilter(laq.AttributeFilter),
            _ => false
        };
    }

    /// <summary>
    /// Estimates the selectivity of a filter (0.0 = very selective, 1.0 = not selective).
    /// Used to optimize query ordering.
    /// </summary>
    public double EstimateSelectivity(FilterNode? filter)
    {
        if (filter == null) return 1.0;

        return filter switch
        {
            // Component type is usually very selective
            ComponentTypeFilterNode => 0.1,

            // HAS filters with specific solutions are selective
            HasFilterNode => 0.3,
            HasAnyFilterNode hasAny => Math.Min(0.5, 0.2 * hasAny.Solutions.Count),
            HasAllFilterNode hasAll => Math.Max(0.1, 0.3 / hasAll.Solutions.Count),
            HasNoneFilterNode hasNone => 0.7,

            // Attribute filters depend on operator
            AttributeFilterNode attr => attr.Operator switch
            {
                StringOperator.Equals => 0.1,
                StringOperator.BeginsWith => 0.2,
                StringOperator.EndsWith => 0.3,
                StringOperator.Contains => 0.4,
                _ => 0.5
            },

            // Logical operators combine children
            AndFilterNode and => and.Children.Select(EstimateSelectivity).Aggregate(1.0, (a, b) => a * b),
            OrFilterNode or => or.Children.Select(EstimateSelectivity).Max(),
            NotFilterNode not => 1.0 - EstimateSelectivity(not.Child),

            // Default
            _ => 0.5
        };
    }
}
