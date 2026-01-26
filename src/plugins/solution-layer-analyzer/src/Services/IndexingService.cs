using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Models;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for indexing solutions and their component layers.
/// </summary>
public class IndexingService
{
    private readonly AnalyzerDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly ServiceClient _serviceClient;
    private readonly IPluginContext _pluginContext;

    public IndexingService(
        AnalyzerDbContext dbContext,
        ILogger logger,
        ServiceClient serviceClient,
        IPluginContext pluginContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceClient = serviceClient;
        _pluginContext = pluginContext;
    }

    public async Task<IndexResponse> IndexAsync(IndexRequest request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var stats = new IndexStats();

        try
        {
            // Phase 1: Discover and index solutions
            await EmitProgressAsync("solutions", 0, "Discovering solutions...");
            
            var allSolutionNames = request.SourceSolutions.Concat(request.TargetSolutions).Distinct().ToList();
            var solutions = await DiscoverSolutionsAsync(allSolutionNames, request, cancellationToken);
            
            stats = stats with { Solutions = solutions.Count };
            await EmitProgressAsync("solutions", 50, $"Found {solutions.Count} solutions");

            // Phase 2: Discover components from solutions
            await EmitProgressAsync("components", 0, "Discovering components...");
            
            var components = await DiscoverComponentsAsync(
                solutions.Where(s => s.IsTarget).ToList(),
                request.IncludeComponentTypes,
                cancellationToken);
            
            stats = stats with { Components = components.Count };
            await EmitProgressAsync("components", 50, $"Found {components.Count} components");

            // Phase 3: Build layer stacks for each component
            await EmitProgressAsync("layers", 0, "Building layer stacks...");
            
            var layerCount = await BuildLayerStacksAsync(components, cancellationToken);
            
            stats = stats with { Layers = layerCount };
            await EmitProgressAsync("layers", 100, $"Built {layerCount} layers");

            // Mark indexing as complete
            await EmitProgressAsync("complete", 100, "Indexing complete");

            return new IndexResponse
            {
                Stats = stats,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during indexing");
            throw;
        }
    }

    private async Task<List<Solution>> DiscoverSolutionsAsync(
        List<string> solutionNames,
        IndexRequest request,
        CancellationToken cancellationToken)
    {
        var solutions = new List<Solution>();

        try
        {
            // Build query for solutions
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid", "uniquename", "friendlyname", "version", "ismanaged", "publisherid")
            };

            // Add filter for solution names
            var filter = new FilterExpression(LogicalOperator.Or);
            foreach (var name in solutionNames)
            {
                filter.AddCondition("uniquename", ConditionOperator.Equal, name);
            }
            query.Criteria = filter;

            _logger.LogInformation("Querying solutions with {Count} names", solutionNames.Count);

            var results = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

            foreach (var entity in results.Entities)
            {
                var uniqueName = entity.GetAttributeValue<string>("uniquename") ?? "";
                var solution = new Solution
                {
                    SolutionId = entity.Id,
                    UniqueName = uniqueName,
                    FriendlyName = entity.GetAttributeValue<string>("friendlyname") ?? uniqueName,
                    Publisher = "Unknown", // TODO: Lookup publisher name from publisherid
                    IsManaged = entity.GetAttributeValue<bool>("ismanaged"),
                    Version = entity.GetAttributeValue<string>("version") ?? "1.0.0.0",
                    IsSource = request.SourceSolutions.Contains(uniqueName),
                    IsTarget = request.TargetSolutions.Contains(uniqueName)
                };

                solutions.Add(solution);
                _dbContext.Solutions.Add(solution);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Indexed {Count} solutions", solutions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering solutions");
        }

        return solutions;
    }

    private async Task<List<Component>> DiscoverComponentsAsync(
        List<Solution> targetSolutions,
        List<string> componentTypes,
        CancellationToken cancellationToken)
    {
        var components = new List<Component>();

        try
        {
            foreach (var solution in targetSolutions)
            {
                var query = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("componenttype", "objectid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("solutionid", ConditionOperator.Equal, solution.SolutionId)
                        }
                    }
                };

                var results = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);

                foreach (var entity in results.Entities)
                {
                    var objectId = entity.GetAttributeValue<Guid>("objectid");
                    var componentTypeCode = entity.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0;

                    // Check if we already have this component
                    if (components.Any(c => c.ObjectId == objectId))
                    {
                        continue;
                    }

                    var componentTypeName = MapComponentTypeCodeToName(componentTypeCode);

                    // Filter by requested component types
                    if (componentTypes.Count > 0 && !componentTypes.Contains(componentTypeName))
                    {
                        continue;
                    }

                    var component = new Component
                    {
                        ComponentId = Guid.NewGuid(),
                        ObjectId = objectId,
                        ComponentType = componentTypeName,
                        ComponentTypeCode = componentTypeCode,
                        LogicalName = $"component_{objectId:N}",
                        DisplayName = $"Component {componentTypeName}"
                    };

                    components.Add(component);
                    _dbContext.Components.Add(component);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Discovered {Count} components", components.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering components");
        }

        return components;
    }

    private async Task<int> BuildLayerStacksAsync(
        List<Component> components,
        CancellationToken cancellationToken)
    {
        var layerCount = 0;

        try
        {
            foreach (var component in components)
            {
                var componentTypeName = GetDataverseComponentTypeName(component.ComponentType);
                
                // Query msdyn_componentlayer for this component
                var query = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet("msdyn_componentlayerid", "msdyn_solutionname", "msdyn_order", "msdyn_publishername"),
                    Criteria = new FilterExpression(LogicalOperator.And)
                    {
                        Conditions =
                        {
                            new ConditionExpression("msdyn_componentid", ConditionOperator.Equal, component.ObjectId),
                            new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, componentTypeName)
                        }
                    },
                    Orders = { new OrderExpression("msdyn_order", OrderType.Ascending) }
                };

                EntityCollection results;
                try
                {
                    results = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "No layers found for component {ComponentId}", component.ComponentId);
                    continue;
                }

                foreach (var entity in results.Entities)
                {
                    // Get ordinal from msdyn_order field (fix: don't manually increment)
                    var ordinal = entity.GetAttributeValue<int>("msdyn_order");
                    
                    var layer = new Layer
                    {
                        LayerId = Guid.NewGuid(),
                        ComponentId = component.ComponentId,
                        Ordinal = ordinal,  // Use the value from Dataverse
                        SolutionName = entity.GetAttributeValue<string>("msdyn_solutionname") ?? "Unknown",
                        Publisher = entity.Contains("msdyn_publishername") ? 
                            entity.GetAttributeValue<string>("msdyn_publishername") ?? "Unknown" : "Unknown",
                        IsManaged = true,
                        Version = "1.0.0.0",
                        CreatedOn = DateTimeOffset.UtcNow
                    };

                    var solution = await _dbContext.Solutions
                        .FirstOrDefaultAsync(s => s.UniqueName == layer.SolutionName, cancellationToken);
                    
                    if (solution != null)
                    {
                        layer.SolutionId = solution.SolutionId;
                        layer.IsManaged = solution.IsManaged;
                        layer.Version = solution.Version;
                        layer.Publisher = solution.Publisher;
                    }

                    _dbContext.Layers.Add(layer);
                    layerCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Built {Count} layer records", layerCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building layer stacks");
        }

        return layerCount;
    }

    private async Task EmitProgressAsync(string phase, int percent, string message)
    {
        var progressEvent = new PluginEvent
        {
            Type = "plugin:sla:progress",
            Payload = JsonSerializer.Serialize(new { phase, percent, message }),
            Timestamp = DateTimeOffset.UtcNow
        };

        _pluginContext.EmitEvent(progressEvent);
    }

    private static string MapComponentTypeCodeToName(int typeCode)
    {
        return typeCode switch
        {
            1 => "Entity",
            2 => "Attribute",
            24 => "SystemForm",  // Form (older code)
            26 => "SavedQuery",
            50 => "RibbonCustomization",
            60 => "SystemForm",  // Form (primary code used in newer solutions)
            61 => "WebResource",
            80 => "AppModule",
            92 => "SDKMessageProcessingStep",
            _ => $"Unknown_{typeCode}"
        };
    }

    private static string GetDataverseComponentTypeName(string componentType)
    {
        return componentType switch
        {
            "Entity" => "Entity",
            "Attribute" => "Attribute",
            "SystemForm" => "SystemForm",
            "SavedQuery" => "SavedQuery",
            "Form" => "SystemForm",
            "View" => "SavedQuery",
            _ => componentType
        };
    }
}
