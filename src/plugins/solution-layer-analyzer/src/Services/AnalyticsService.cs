using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for computing analytical datasets and metrics.
/// Backend decides what is problematic - precomputes violations, risk scores, and aggregations.
/// </summary>
public class AnalyticsService
{
    private readonly ILogger _logger;

    public AnalyticsService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compute comprehensive analytics from indexed data
    /// </summary>
    public async Task<GetAnalyticsResponse> ComputeAnalyticsAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Computing comprehensive analytics...");

        var response = new GetAnalyticsResponse
        {
            SolutionOverlaps = await ComputeSolutionOverlapsAsync(dbContext, cancellationToken),
            ComponentRisks = await ComputeComponentRisksAsync(dbContext, cancellationToken),
            Violations = await DetectViolationsAsync(dbContext, cancellationToken),
            SolutionMetrics = await ComputeSolutionMetricsAsync(dbContext, cancellationToken),
            NetworkData = await BuildNetworkGraphDataAsync(dbContext, cancellationToken),
            HierarchyData = await BuildHierarchyDataAsync(dbContext, cancellationToken),
            ChordData = await BuildChordDataAsync(dbContext, cancellationToken),
            UpSetData = await BuildUpSetDataAsync(dbContext, cancellationToken)
        };

        _logger.LogInformation("Analytics computation complete");
        return response;
    }

    /// <summary>
    /// Compute solution-to-solution overlap matrix
    /// </summary>
    private async Task<SolutionOverlapMatrix> ComputeSolutionOverlapsAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.ToListAsync(cancellationToken);
        var components = await dbContext.Components
            .Include(c => c.Layers)
            .ToListAsync(cancellationToken);

        var matrix = new Dictionary<string, Dictionary<string, int>>();
        var detailedOverlaps = new List<SolutionOverlapDetail>();

        // Initialize matrix
        foreach (var sol1 in solutions)
        {
            matrix[sol1.UniqueName] = new Dictionary<string, int>();
            foreach (var sol2 in solutions)
            {
                matrix[sol1.UniqueName][sol2.UniqueName] = 0;
            }
        }

        // Compute overlaps
        foreach (var component in components)
        {
            var solutionNames = component.Layers.Select(l => l.SolutionName).Distinct().ToList();
            
            // Count pairwise overlaps
            for (int i = 0; i < solutionNames.Count; i++)
            {
                for (int j = i + 1; j < solutionNames.Count; j++)
                {
                    var sol1 = solutionNames[i];
                    var sol2 = solutionNames[j];
                    
                    if (matrix.ContainsKey(sol1) && matrix[sol1].ContainsKey(sol2))
                    {
                        matrix[sol1][sol2]++;
                        matrix[sol2][sol1]++; // Symmetric
                    }
                }
            }
        }

        // Build detailed overlaps with severity assessment
        foreach (var sol1 in solutions)
        {
            foreach (var sol2 in solutions)
            {
                if (sol1.UniqueName == sol2.UniqueName) continue;
                if (matrix[sol1.UniqueName][sol2.UniqueName] == 0) continue;

                // Already added in reverse? Skip to avoid duplicates
                if (detailedOverlaps.Any(d => d.Solution1 == sol2.UniqueName && d.Solution2 == sol1.UniqueName))
                    continue;

                var overlappingComponents = components.Where(c =>
                    c.Layers.Any(l => l.SolutionName == sol1.UniqueName) &&
                    c.Layers.Any(l => l.SolutionName == sol2.UniqueName)
                ).ToList();

                var detail = new SolutionOverlapDetail
                {
                    Solution1 = sol1.UniqueName,
                    Solution2 = sol2.UniqueName,
                    TotalOverlap = overlappingComponents.Count,
                    ManagedOverlap = overlappingComponents.Count(c => c.Layers.All(l => l.IsManaged)),
                    UnmanagedOverlap = overlappingComponents.Count(c => !c.Layers.All(l => l.IsManaged)),
                    ComponentTypeBreakdown = overlappingComponents
                        .GroupBy(c => c.ComponentType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    Severity = DetermineSeverity(overlappingComponents.Count, overlappingComponents.Any(c => !c.Layers.All(l => l.IsManaged)))
                };

                detailedOverlaps.Add(detail);
            }
        }

        return new SolutionOverlapMatrix
        {
            Matrix = matrix,
            DetailedOverlaps = detailedOverlaps
        };
    }

    /// <summary>
    /// Compute risk scores for all components
    /// </summary>
    private async Task<List<ComponentRiskSummary>> ComputeComponentRisksAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var components = await dbContext.Components
            .Include(c => c.Layers.OrderBy(l => l.Ordinal))
            .ToListAsync(cancellationToken);

        var risks = new List<ComponentRiskSummary>();

        foreach (var component in components)
        {
            var layers = component.Layers.OrderBy(l => l.Ordinal).ToList();
            if (!layers.Any()) continue;

            var topLayer = layers.Last();
            var baseLayer = layers.First();
            var hasUnmanaged = layers.Any(l => !l.IsManaged);
            var violations = new List<string>();

            // Detect violations
            if (hasUnmanaged && layers.Count > 1)
            {
                violations.Add("unmanaged_override");
            }
            if (layers.Count > 5)
            {
                violations.Add("excessive_depth");
            }

            // Calculate risk score (0-100)
            int riskScore = 0;
            riskScore += layers.Count * 10; // More layers = more risk
            riskScore += hasUnmanaged ? 30 : 0; // Unmanaged = high risk
            riskScore += violations.Count * 20;
            riskScore = Math.Min(100, riskScore);

            risks.Add(new ComponentRiskSummary
            {
                ComponentId = component.ComponentId.ToString(),
                ComponentType = component.ComponentType,
                LogicalName = component.LogicalName,
                DisplayName = component.DisplayName,
                RiskScore = riskScore,
                LayerDepth = layers.Count,
                TopmostSolution = topLayer.SolutionName,
                BaseSolution = baseLayer.SolutionName,
                HasUnmanagedOverride = hasUnmanaged,
                ViolationFlags = violations,
                ModifyingSolutions = layers.Select(l => l.SolutionName).Distinct().ToList()
            });
        }

        return risks.OrderByDescending(r => r.RiskScore).ToList();
    }

    /// <summary>
    /// Detect violations based on business rules
    /// </summary>
    private async Task<List<ViolationItem>> DetectViolationsAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var components = await dbContext.Components
            .Include(c => c.Layers.OrderBy(l => l.Ordinal))
            .ToListAsync(cancellationToken);

        var violations = new List<ViolationItem>();
        int violationId = 1;

        foreach (var component in components)
        {
            var layers = component.Layers.OrderBy(l => l.Ordinal).ToList();
            if (!layers.Any()) continue;

            // Violation: Unmanaged override
            var unmanagedLayers = layers.Where(l => !l.IsManaged).ToList();
            if (unmanagedLayers.Any() && layers.Count > 1)
            {
                violations.Add(new ViolationItem
                {
                    ViolationId = $"V{violationId++}",
                    Type = "unmanaged_override",
                    Severity = "high",
                    ComponentId = component.ComponentId.ToString(),
                    ComponentName = component.DisplayName ?? component.LogicalName,
                    Description = $"Component has {unmanagedLayers.Count} unmanaged layer(s) overriding managed layers",
                    AffectedSolutions = unmanagedLayers.Select(l => l.SolutionName).ToList()
                });
            }

            // Violation: Excessive depth
            if (layers.Count > 5)
            {
                violations.Add(new ViolationItem
                {
                    ViolationId = $"V{violationId++}",
                    Type = "excessive_depth",
                    Severity = "medium",
                    ComponentId = component.ComponentId.ToString(),
                    ComponentName = component.DisplayName ?? component.LogicalName,
                    Description = $"Component has {layers.Count} layers (exceeds recommended maximum of 5)",
                    AffectedSolutions = layers.Select(l => l.SolutionName).ToList()
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// Compute per-solution metrics
    /// </summary>
    private async Task<List<SolutionMetrics>> ComputeSolutionMetricsAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.ToListAsync(cancellationToken);
        var layers = await dbContext.Layers
            .Include(l => l.Component)
            .ToListAsync(cancellationToken);

        var metrics = new List<SolutionMetrics>();

        foreach (var solution in solutions)
        {
            var solutionLayers = layers.Where(l => l.SolutionName == solution.UniqueName).ToList();
            var componentIds = solutionLayers.Select(l => l.ComponentId).Distinct().ToList();
            var components = await dbContext.Components
                .Where(c => componentIds.Contains(c.ComponentId))
                .Include(c => c.Layers)
                .ToListAsync(cancellationToken);

            // Find other solutions that share components
            var overlaps = components
                .SelectMany(c => c.Layers.Select(l => l.SolutionName))
                .Where(s => s != solution.UniqueName)
                .Distinct()
                .ToList();

            metrics.Add(new SolutionMetrics
            {
                SolutionName = solution.UniqueName,
                IsManaged = solution.IsManaged,
                Publisher = solution.Publisher ?? "Unknown",
                TotalLayers = solutionLayers.Count,
                UnmanagedLayers = solutionLayers.Count(l => !l.IsManaged),
                ComponentsModified = componentIds.Count,
                ComponentTypeBreakdown = solutionLayers
                    .GroupBy(l => l.Component?.ComponentType ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count()),
                ViolationCount = 0, // Will be filled by cross-referencing violations
                OverlapsWith = overlaps
            });
        }

        return metrics;
    }

    /// <summary>
    /// Build network graph data for force-directed layout
    /// </summary>
    private async Task<NetworkGraphData> BuildNetworkGraphDataAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.ToListAsync(cancellationToken);
        var components = await dbContext.Components
            .Include(c => c.Layers)
            .ToListAsync(cancellationToken);

        var nodes = new List<NetworkNode>();
        var links = new List<NetworkLink>();

        // Add solution nodes
        foreach (var solution in solutions)
        {
            nodes.Add(new NetworkNode
            {
                Id = $"sol_{solution.SolutionId}",
                Label = solution.UniqueName,
                Type = "solution",
                Group = solution.IsManaged ? "managed" : "unmanaged",
                Size = components.Count(c => c.Layers.Any(l => l.SolutionName == solution.UniqueName)),
                Metadata = new Dictionary<string, object>
                {
                    ["isManaged"] = solution.IsManaged,
                    ["publisher"] = solution.Publisher ?? ""
                }
            });
        }

        // Add component nodes (only high-risk or heavily layered ones to avoid clutter)
        var importantComponents = components.Where(c => c.Layers.Count >= 2).ToList();
        foreach (var component in importantComponents)
        {
            nodes.Add(new NetworkNode
            {
                Id = $"comp_{component.ComponentId}",
                Label = component.DisplayName ?? component.LogicalName,
                Type = "component",
                Group = component.ComponentType,
                Size = component.Layers.Count * 2,
                Metadata = new Dictionary<string, object>
                {
                    ["componentType"] = component.ComponentType,
                    ["layerCount"] = component.Layers.Count
                }
            });

            // Add links from solutions to this component
            // Only create links for layers with valid SolutionIds (skip Guid.Empty)
            foreach (var layer in component.Layers.Where(l => l.SolutionId != Guid.Empty))
            {
                links.Add(new NetworkLink
                {
                    Source = $"sol_{layer.SolutionId}",
                    Target = $"comp_{component.ComponentId}",
                    Value = 1,
                    Type = "modifies"
                });
            }
        }

        return new NetworkGraphData
        {
            Nodes = nodes,
            Links = links
        };
    }

    /// <summary>
    /// Build hierarchical tree structure
    /// </summary>
    private async Task<HierarchyData> BuildHierarchyDataAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.OrderBy(s => s.UniqueName).ToListAsync(cancellationToken);
        var components = await dbContext.Components
            .Include(c => c.Layers.OrderBy(l => l.Ordinal))
            .ToListAsync(cancellationToken);

        // Find base solutions (those with fewest dependencies)
        var baseSolution = solutions.FirstOrDefault();
        if (baseSolution == null)
        {
            return new HierarchyData { Root = new HierarchyNode { Id = "root", Label = "No Data" } };
        }

        var root = new HierarchyNode
        {
            Id = $"sol_{baseSolution.SolutionId}",
            Label = baseSolution.UniqueName,
            Type = "solution",
            Metadata = new Dictionary<string, object>
            {
                ["isManaged"] = baseSolution.IsManaged
            },
            Children = new List<HierarchyNode>()
        };

        // Add components modified by base solution
        var baseComponents = components.Where(c =>
            c.Layers.Any(l => l.SolutionName == baseSolution.UniqueName)
        ).ToList();

        foreach (var component in baseComponents)
        {
            var componentNode = new HierarchyNode
            {
                Id = $"comp_{component.ComponentId}",
                Label = component.DisplayName ?? component.LogicalName,
                Type = "component",
                Metadata = new Dictionary<string, object>
                {
                    ["componentType"] = component.ComponentType,
                    ["layerCount"] = component.Layers.Count
                },
                Children = new List<HierarchyNode>()
            };

            // Add layering solutions as children
            var layeringSolutions = component.Layers
                .Where(l => l.SolutionName != baseSolution.UniqueName)
                .Select(l => l.SolutionName)
                .Distinct();

            foreach (var solName in layeringSolutions)
            {
                componentNode.Children.Add(new HierarchyNode
                {
                    Id = $"layer_{component.ComponentId}_{solName}",
                    Label = solName,
                    Type = "layer",
                    Children = new List<HierarchyNode>()
                });
            }

            root.Children.Add(componentNode);
        }

        return new HierarchyData { Root = root };
    }

    /// <summary>
    /// Build chord diagram data
    /// </summary>
    private async Task<ChordDiagramData> BuildChordDataAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.OrderBy(s => s.UniqueName).ToListAsync(cancellationToken);
        var components = await dbContext.Components
            .Include(c => c.Layers)
            .ToListAsync(cancellationToken);

        var solutionNames = solutions.Select(s => s.UniqueName).ToList();
        var n = solutionNames.Count;
        var matrix = new List<List<int>>();

        // Initialize matrix
        for (int i = 0; i < n; i++)
        {
            matrix.Add(Enumerable.Repeat(0, n).ToList());
        }

        var details = new Dictionary<string, ChordDetail>();

        // Fill matrix
        foreach (var component in components)
        {
            var solNames = component.Layers.Select(l => l.SolutionName).Distinct().ToList();
            
            for (int i = 0; i < solNames.Count; i++)
            {
                for (int j = i + 1; j < solNames.Count; j++)
                {
                    var idx1 = solutionNames.IndexOf(solNames[i]);
                    var idx2 = solutionNames.IndexOf(solNames[j]);
                    
                    if (idx1 >= 0 && idx2 >= 0)
                    {
                        matrix[idx1][idx2]++;
                        matrix[idx2][idx1]++;

                        var key = $"{solNames[i]}_{solNames[j]}";
                        if (!details.ContainsKey(key))
                        {
                            details[key] = new ChordDetail
                            {
                                From = solNames[i],
                                To = solNames[j],
                                Count = 0,
                                Components = new List<string>()
                            };
                        }
                        details[key].Count++;
                        details[key].Components.Add(component.DisplayName ?? component.LogicalName);
                    }
                }
            }
        }

        return new ChordDiagramData
        {
            Solutions = solutionNames,
            Matrix = matrix,
            Details = details
        };
    }

    /// <summary>
    /// Build UpSet plot data
    /// </summary>
    private async Task<UpSetPlotData> BuildUpSetDataAsync(AnalyzerDbContext dbContext, CancellationToken cancellationToken)
    {
        var solutions = await dbContext.Solutions.OrderBy(s => s.UniqueName).ToListAsync(cancellationToken);
        var components = await dbContext.Components
            .Include(c => c.Layers)
            .ToListAsync(cancellationToken);

        var solutionNames = solutions.Select(s => s.UniqueName).ToList();
        var intersections = new Dictionary<string, SetIntersection>();

        foreach (var component in components)
        {
            var componentSolutions = component.Layers
                .Select(l => l.SolutionName)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            if (!componentSolutions.Any()) continue;

            var key = string.Join("|", componentSolutions);
            
            if (!intersections.ContainsKey(key))
            {
                intersections[key] = new SetIntersection
                {
                    Sets = componentSolutions,
                    Size = 0,
                    Components = new List<string>(),
                    Degree = componentSolutions.Count
                };
            }

            intersections[key].Size++;
            intersections[key].Components.Add(component.DisplayName ?? component.LogicalName);
        }

        return new UpSetPlotData
        {
            Sets = solutionNames,
            Intersections = intersections.Values.OrderByDescending(i => i.Size).ToList()
        };
    }

    private string DetermineSeverity(int overlapCount, bool hasUnmanaged)
    {
        if (hasUnmanaged && overlapCount > 10) return "critical";
        if (hasUnmanaged || overlapCount > 20) return "high";
        if (overlapCount > 10) return "normal";
        return "low";
    }
}
