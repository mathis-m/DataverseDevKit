using Microsoft.EntityFrameworkCore;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Filters;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for querying indexed components with filtering.
/// </summary>
public class QueryService
{
    private readonly AnalyzerDbContext _dbContext;
    private readonly FilterEvaluator _filterEvaluator;

    public QueryService(AnalyzerDbContext dbContext)
    {
        _dbContext = dbContext;
        _filterEvaluator = new FilterEvaluator();
    }

    public async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        // Get all components with layers
        var components = await _dbContext.Components
            .Include(c => c.Layers)
            .ToListAsync(cancellationToken);

        // Apply filters
        if (request.Filters != null)
        {
            components = components
                .Where(c => _filterEvaluator.Evaluate(request.Filters, c))
                .ToList();
        }

        var total = components.Count;

        // Apply sorting
        if (request.Sort.Count > 0)
        {
            var sortField = request.Sort[0].Field;
            var sortDir = request.Sort[0].Dir;

            components = sortField.ToLowerInvariant() switch
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
                _ => components
            };
        }

        // Apply paging
        var pagedComponents = components
            .Skip(request.Paging.Skip)
            .Take(request.Paging.Take)
            .ToList();

        // Map to response
        var rows = pagedComponents.Select(c => new ComponentResult
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

        return new QueryResponse
        {
            Rows = rows,
            Total = total
        };
    }
}
