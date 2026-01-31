using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Filters;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for querying indexed components with filtering.
/// Uses SQL translation where possible for improved performance.
/// </summary>
public class QueryService
{
    private readonly AnalyzerDbContext _dbContext;
    private readonly FilterEvaluator _filterEvaluator;
    private readonly QueryPlanBuilder _planBuilder;

    public QueryService(AnalyzerDbContext dbContext)
    {
        _dbContext = dbContext;
        _filterEvaluator = new FilterEvaluator();
        _planBuilder = new QueryPlanBuilder();
    }

    public async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        var stats = new QueryPlanStats();
        var totalStopwatch = Stopwatch.StartNew();

        // Build query plan
        var plan = _planBuilder.BuildPlan(request.Filters);
        stats.PlanDescription = plan.Description;

        // Execute pre-fetch operations if needed
        PreFetchResults? preFetchResults = null;
        if (plan.RequiresPreFetch)
        {
            var preFetchStopwatch = Stopwatch.StartNew();
            preFetchResults = await ExecutePreFetchOperationsAsync(plan.PreFetchOperations, cancellationToken);
            stats.PreFetchDuration = preFetchStopwatch.Elapsed;
        }

        // Build the base query
        var query = _dbContext.Components
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Layers)
            .ThenInclude(l => l.Attributes)
            .AsQueryable();

        // Apply SQL filter if available
        var sqlStopwatch = Stopwatch.StartNew();
        if (plan.SqlExpression != null)
        {
            // Rebuild expression with pre-fetch results if needed
            if (preFetchResults != null)
            {
                var builder = new SqlFilterBuilder(
                    preFetchResults.OrdinalRanges,
                    preFetchResults.ResolvedSolutionNames);
                var enhancedExpression = builder.BuildExpression(request.Filters);
                query = query.Where(enhancedExpression);
            }
            else
            {
                query = query.Where(plan.SqlExpression);
            }
        }

        // Apply sorting at SQL level if possible
        query = ApplySorting(query, request.Sort);

        List<Component> components;

        if (plan.InMemoryFilter == null)
        {
            // Fully SQL-translatable: use SQL pagination
            var totalCount = await query.CountAsync(cancellationToken);
            stats.SqlQueryDuration = sqlStopwatch.Elapsed;
            stats.RowsFromSql = totalCount;

            var pagedQuery = query
                .Skip(request.Paging.Skip)
                .Take(request.Paging.Take);

            components = await pagedQuery.ToListAsync(cancellationToken);
            stats.RowsAfterFilter = totalCount;
            stats.UsedInMemoryFilter = false;

            var rows = MapToResults(components);

            totalStopwatch.Stop();
            stats.TotalDuration = totalStopwatch.Elapsed;

            return new QueryResponse
            {
                QueryId = request.QueryId,
                Rows = rows,
                Total = totalCount,
                Stats = stats
            };
        }
        else
        {
            // Hybrid: fetch SQL-filtered results, then apply in-memory filter
            components = await query.ToListAsync(cancellationToken);
            stats.SqlQueryDuration = sqlStopwatch.Elapsed;
            stats.RowsFromSql = components.Count;

            // Apply in-memory filter
            var inMemoryStopwatch = Stopwatch.StartNew();
            components = components
                .Where(c => _filterEvaluator.Evaluate(plan.InMemoryFilter, c))
                .ToList();
            stats.InMemoryFilterDuration = inMemoryStopwatch.Elapsed;
            stats.RowsAfterFilter = components.Count;
            stats.UsedInMemoryFilter = true;

            var total = components.Count;

            // Apply in-memory sorting (may have been partially applied in SQL)
            components = ApplyInMemorySorting(components, request.Sort);

            // Apply paging
            var pagedComponents = components
                .Skip(request.Paging.Skip)
                .Take(request.Paging.Take)
                .ToList();

            var rows = MapToResults(pagedComponents);

            totalStopwatch.Stop();
            stats.TotalDuration = totalStopwatch.Elapsed;

            return new QueryResponse
            {
                QueryId = request.QueryId,
                Rows = rows,
                Total = total,
                Stats = stats
            };
        }
    }

    private async Task<PreFetchResults> ExecutePreFetchOperationsAsync(
        List<PreFetchOperation> operations,
        CancellationToken cancellationToken)
    {
        var results = new PreFetchResults();

        foreach (var op in operations)
        {
            switch (op.Type)
            {
                case PreFetchType.OrdinalRanges:
                    if (op.SolutionNames != null)
                    {
                        var ordinals = await _dbContext.Layers
                            .Where(l => op.SolutionNames.Contains(l.SolutionName))
                            .GroupBy(l => l.SolutionName)
                            .Select(g => new
                            {
                                SolutionName = g.Key,
                                MinOrdinal = g.Min(l => l.Ordinal),
                                MaxOrdinal = g.Max(l => l.Ordinal)
                            })
                            .ToListAsync(cancellationToken);

                        foreach (var o in ordinals)
                        {
                            results.OrdinalRanges[o.SolutionName] = (o.MinOrdinal, o.MaxOrdinal);
                        }
                    }
                    break;

                case PreFetchType.SolutionNameResolution:
                    if (op.SolutionQuery != null)
                    {
                        var matchingSolutions = await ResolveSolutionQueryAsync(
                            op.SolutionQuery,
                            cancellationToken);
                        results.ResolvedSolutionNames[op.ResultKey] = matchingSolutions;
                    }
                    break;

                case PreFetchType.ComponentIdSubquery:
                    // Reserved for future complex subqueries
                    break;
            }
        }

        return results;
    }

    private async Task<List<string>> ResolveSolutionQueryAsync(
        SolutionQueryNode query,
        CancellationToken cancellationToken)
    {
        // Get distinct solution names from layers and apply the query
        var allSolutionNames = await _dbContext.Layers
            .Select(l => l.SolutionName)
            .Distinct()
            .ToListAsync(cancellationToken);

        return allSolutionNames
            .Where(name => EvaluateSolutionQuery(query, name))
            .ToList();
    }

    private bool EvaluateSolutionQuery(SolutionQueryNode query, string solutionName)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;

        return query.Operator switch
        {
            StringOperator.Equals => solutionName.Equals(query.Value, comparison),
            StringOperator.NotEquals => !solutionName.Equals(query.Value, comparison),
            StringOperator.Contains => solutionName.Contains(query.Value, comparison),
            StringOperator.NotContains => !solutionName.Contains(query.Value, comparison),
            StringOperator.BeginsWith => solutionName.StartsWith(query.Value, comparison),
            StringOperator.NotBeginsWith => !solutionName.StartsWith(query.Value, comparison),
            StringOperator.EndsWith => solutionName.EndsWith(query.Value, comparison),
            StringOperator.NotEndsWith => !solutionName.EndsWith(query.Value, comparison),
            _ => true
        };
    }

    private IQueryable<Component> ApplySorting(IQueryable<Component> query, List<SortSettings> sorts)
    {
        if (sorts.Count == 0)
        {
            return query;
        }

        var sortField = sorts[0].Field;
        var sortDir = sorts[0].Dir;
        var isAsc = sortDir == "asc";

        return sortField.ToLowerInvariant() switch
        {
            "componenttype" => isAsc
                ? query.OrderBy(c => c.ComponentType)
                : query.OrderByDescending(c => c.ComponentType),
            "logicalname" => isAsc
                ? query.OrderBy(c => c.LogicalName)
                : query.OrderByDescending(c => c.LogicalName),
            "displayname" => isAsc
                ? query.OrderBy(c => c.DisplayName)
                : query.OrderByDescending(c => c.DisplayName),
            "tablelogicalname" => isAsc
                ? query.OrderBy(c => c.TableLogicalName)
                : query.OrderByDescending(c => c.TableLogicalName),
            _ => query
        };
    }

    private List<Component> ApplyInMemorySorting(List<Component> components, List<SortSettings> sorts)
    {
        if (sorts.Count == 0)
        {
            return components;
        }

        var sortField = sorts[0].Field;
        var sortDir = sorts[0].Dir;

        return sortField.ToLowerInvariant() switch
        {
            "componenttype" => sortDir == "asc"
                ? components.OrderBy(c => c.ComponentType).ToList()
                : components.OrderByDescending(c => c.ComponentType).ToList(),
            "logicalname" => sortDir == "asc"
                ? components.OrderBy(c => c.LogicalName).ToList()
                : components.OrderByDescending(c => c.LogicalName).ToList(),
            "displayname" => sortDir == "asc"
                ? components.OrderBy(c => c.DisplayName).ToList()
                : components.OrderByDescending(c => c.DisplayName).ToList(),
            "tablelogicalname" => sortDir == "asc"
                ? components.OrderBy(c => c.TableLogicalName).ToList()
                : components.OrderByDescending(c => c.TableLogicalName).ToList(),
            _ => components
        };
    }

    private List<ComponentResult> MapToResults(List<Component> components)
    {
        return components.Select(c => new ComponentResult
        {
            ComponentId = c.ComponentId,
            ComponentType = c.ComponentType,
            LogicalName = c.LogicalName,
            DisplayName = c.DisplayName,
            LayerSequence = c.Layers.OrderBy(l => l.Ordinal).Select(l => l.SolutionName).ToList(),
            IsManaged = c.Layers.Any(l => l.IsManaged),
            Publisher = c.Layers.FirstOrDefault()?.Publisher,
            TableLogicalName = c.TableLogicalName
        }).ToList();
    }
}
