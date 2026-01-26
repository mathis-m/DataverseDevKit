using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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
    private readonly IDataverseClient _dataverseClient;
    private readonly IPluginContext _pluginContext;

    public IndexingService(
        AnalyzerDbContext dbContext,
        ILogger logger,
        IDataverseClient dataverseClient,
        IPluginContext pluginContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _dataverseClient = dataverseClient;
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

        // Build OData filter for solution names
        var filterConditions = solutionNames.Select(name => $"uniquename eq '{name}'");
        var filter = string.Join(" or ", filterConditions);
        var query = $"$select=solutionid,uniquename,friendlyname,version,ismanaged&$filter={filter}";

        _logger.LogInformation("Querying solutions with filter: {Filter}", query);

        var result = await _dataverseClient.QueryAsync("solution", query, cancellationToken);

        if (!result.Success || result.Data == null)
        {
            _logger.LogWarning("Failed to query solutions: {Error}", result.Error);
            return solutions;
        }

        // Parse JSON response
        var jsonDoc = JsonDocument.Parse(result.Data);
        var solutionArray = jsonDoc.RootElement;

        if (solutionArray.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Unexpected response format from solutions query");
            return solutions;
        }

        foreach (var solutionElement in solutionArray.EnumerateArray())
        {
            var uniqueName = solutionElement.GetProperty("uniquename").GetString() ?? "";
            var solution = new Solution
            {
                SolutionId = Guid.Parse(solutionElement.GetProperty("solutionid").GetString()!),
                UniqueName = uniqueName,
                FriendlyName = solutionElement.GetProperty("friendlyname").GetString() ?? uniqueName,
                Publisher = "Unknown",
                IsManaged = solutionElement.GetProperty("ismanaged").GetBoolean(),
                Version = solutionElement.GetProperty("version").GetString() ?? "1.0.0.0",
                IsSource = request.SourceSolutions.Contains(uniqueName),
                IsTarget = request.TargetSolutions.Contains(uniqueName)
            };

            solutions.Add(solution);
            _dbContext.Solutions.Add(solution);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Indexed {Count} solutions", solutions.Count);

        return solutions;
    }

    private async Task<List<Component>> DiscoverComponentsAsync(
        List<Solution> targetSolutions,
        List<string> componentTypes,
        CancellationToken cancellationToken)
    {
        var components = new List<Component>();

        foreach (var solution in targetSolutions)
        {
            var query = $"$select=componenttype,objectid&$filter=solutionid eq {solution.SolutionId}";
            var result = await _dataverseClient.QueryAsync("solutioncomponent", query, cancellationToken);

            if (!result.Success || result.Data == null)
            {
                _logger.LogWarning("Failed to query components for solution {Solution}", solution.UniqueName);
                continue;
            }

            var jsonDoc = JsonDocument.Parse(result.Data);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var componentElement in jsonDoc.RootElement.EnumerateArray())
            {
                var objectId = Guid.Parse(componentElement.GetProperty("objectid").GetString()!);
                var componentType = componentElement.GetProperty("componenttype").GetInt32();

                if (components.Any(c => c.ObjectId == objectId))
                {
                    continue;
                }

                var componentTypeName = MapComponentTypeCodeToName(componentType);

                if (componentTypes.Count > 0 && !componentTypes.Contains(componentTypeName))
                {
                    continue;
                }

                var component = new Component
                {
                    ComponentId = Guid.NewGuid(),
                    ObjectId = objectId,
                    ComponentType = componentTypeName,
                    ComponentTypeCode = componentType,
                    LogicalName = $"component_{objectId:N}",
                    DisplayName = $"Component {componentTypeName}"
                };

                components.Add(component);
                _dbContext.Components.Add(component);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Discovered {Count} components", components.Count);

        return components;
    }

    private async Task<int> BuildLayerStacksAsync(
        List<Component> components,
        CancellationToken cancellationToken)
    {
        var layerCount = 0;

        foreach (var component in components)
        {
            var componentTypeName = GetDataverseComponentTypeName(component.ComponentType);
            var filter = $"msdyn_componentid eq {component.ObjectId} and msdyn_solutioncomponentname eq '{componentTypeName}'";
            var query = $"$select=msdyn_componentlayerid,msdyn_solutionname,msdyn_order,msdyn_publishername&$filter={filter}&$orderby=msdyn_order asc";

            var result = await _dataverseClient.QueryAsync("msdyn_componentlayer", query, cancellationToken);

            if (!result.Success || result.Data == null)
            {
                _logger.LogDebug("No layers found for component {ComponentId}", component.ComponentId);
                continue;
            }

            var jsonDoc = JsonDocument.Parse(result.Data);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var ordinal = 0;
            foreach (var layerElement in jsonDoc.RootElement.EnumerateArray())
            {
                var layer = new Layer
                {
                    LayerId = Guid.NewGuid(),
                    ComponentId = component.ComponentId,
                    Ordinal = ordinal++,
                    SolutionName = layerElement.GetProperty("msdyn_solutionname").GetString() ?? "Unknown",
                    Publisher = layerElement.TryGetProperty("msdyn_publishername", out var pub) ? 
                        pub.GetString() ?? "Unknown" : "Unknown",
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
