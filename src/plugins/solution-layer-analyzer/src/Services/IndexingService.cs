using System.Diagnostics;
using System.Text.Json;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Exceptions;
using DataverseDevKit.Core.Models;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for indexing solutions and their component layers.
/// </summary>
public class IndexingService
{
    private readonly DbContextOptions<AnalyzerDbContext> _dbContextOptions;
    private readonly ILogger _logger;
    private readonly ServiceClient _serviceClient;
    private readonly IPluginContext _pluginContext;
    private readonly ComponentNameResolver _nameResolver;
    private readonly LayerAttributeExtractor _attributeExtractor;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IndexingService(
        DbContextOptions<AnalyzerDbContext> dbContextOptions,
        ILogger logger,
        ServiceClient serviceClient,
        IPluginContext pluginContext)
    {
        _dbContextOptions = dbContextOptions;
        _logger = logger;
        _serviceClient = serviceClient;
        _pluginContext = pluginContext;
        _nameResolver = new ComponentNameResolver(dbContextOptions, logger, serviceClient);
        
        // Initialize attribute extractor with payload service for formatting
        var payloadService = new PayloadService(serviceClient, logger);
        _attributeExtractor = new LayerAttributeExtractor(logger, payloadService);
    }

    public async Task<IndexResponse> StartIndexAsync(IndexRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();

        try
        {
            // Create operation tracking record
            var operation = new IndexOperation
            {
                OperationId = operationId,
                Status = IndexOperationStatus.InProgress,
                StartedAt = DateTimeOffset.UtcNow
            };

            await using (var dbContext = new AnalyzerDbContext(_dbContextOptions))
            {
                dbContext.IndexOperations.Add(operation);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // Start indexing in background (fire and forget)
            _ = Task.Run(async () => await ExecuteIndexingAsync(operationId, request), CancellationToken.None);

            return new IndexResponse
            {
                OperationId = operationId,
                Started = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting indexing operation");
            return new IndexResponse
            {
                OperationId = operationId,
                Started = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task ExecuteIndexingAsync(Guid operationId, IndexRequest request)
    {
        var warnings = new List<string>();
        var stats = new IndexStats();
        var success = false;
        string? errorMessage = null;

        try
        {
            // Phase 1: Discover and index solutions
            await EmitProgressAsync(operationId, "solutions", 0, "Discovering solutions...");
            
            var allSolutionNames = request.SourceSolutions.Concat(request.TargetSolutions).Distinct().ToList();
            var solutions = await DiscoverSolutionsAsync(allSolutionNames, request, CancellationToken.None);
            
            stats = stats with { Solutions = solutions.Count };
            await EmitProgressAsync(operationId, "solutions", 50, $"Found {solutions.Count} solutions");

            // Phase 2: Discover components from solutions
            await EmitProgressAsync(operationId, "components", 0, "Discovering components...");
            
            var components = await DiscoverComponentsAsync(
                operationId,
                solutions.Where(s => s.IsTarget).ToList(),
                request.IncludeComponentTypes,
                CancellationToken.None);
            
            stats = stats with { Components = components.Count };
            await EmitProgressAsync(operationId, "components", 50, $"Found {components.Count} components");

            // Phase 3: Build layer stacks for each component
            await EmitProgressAsync(operationId, "layers", 0, "Building layer stacks...");
            
            var layerCount = await BuildLayerStacksAsync(operationId, components, CancellationToken.None);
            
            stats = stats with { Layers = layerCount };
            await EmitProgressAsync(operationId, "layers", 100, $"Built {layerCount} layers");

            success = true;
            _logger.LogInformation("Indexing operation {OperationId} completed successfully", operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during indexing operation {OperationId}", operationId);
            errorMessage = ex.Message;
            success = false;
        }
        finally
        {
            // Update operation status in database
            await UpdateOperationStatusAsync(operationId, success, stats, warnings, errorMessage);

            // Emit completion event
            await EmitCompletionEventAsync(operationId, success, stats, warnings, errorMessage);
        }
    }

    private async Task UpdateOperationStatusAsync(
        Guid operationId, 
        bool success, 
        IndexStats stats, 
        List<string> warnings, 
        string? errorMessage)
    {
        try
        {
            await using var dbContext = new AnalyzerDbContext(_dbContextOptions);
            var operation = await dbContext.IndexOperations.FindAsync(operationId);
            if (operation != null)
            {
                operation.Status = success ? IndexOperationStatus.Completed : IndexOperationStatus.Failed;
                operation.CompletedAt = DateTimeOffset.UtcNow;
                operation.StatsJson = success ? JsonSerializer.Serialize(stats, JsonOptions) : null;
                operation.WarningsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings, JsonOptions) : null;
                operation.ErrorMessage = errorMessage;

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update operation status for {OperationId}", operationId);
        }
    }

    private async Task EmitCompletionEventAsync(
        Guid operationId, 
        bool success, 
        IndexStats stats, 
        List<string> warnings, 
        string? errorMessage)
    {
        try
        {
            var completionEvent = new IndexCompletionEvent
            {
                OperationId = operationId,
                Success = success,
                Stats = success ? stats : null,
                Warnings = warnings,
                ErrorMessage = errorMessage
            };

            var pluginEvent = new PluginEvent
            {
                PluginId = "com.ddk.solutionlayeranalyzer",
                Type = "plugin:sla:index-complete",
                Payload = JsonSerializer.Serialize(completionEvent, JsonOptions),
                Timestamp = DateTimeOffset.UtcNow
            };

            _pluginContext.EmitEvent(pluginEvent);
            _logger.LogInformation("Emitted index completion event for operation {OperationId}", operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to emit completion event for {OperationId}", operationId);
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

            await using (var dbContext = new AnalyzerDbContext(_dbContextOptions))
            {
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
                    dbContext.Solutions.Add(solution);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            
            _logger.LogInformation("Indexed {Count} solutions", solutions.Count);
        }
        catch (SessionExpiredException ex)
        {
            _logger.LogError(ex, "Session timed out while discovering solutions");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering solutions");
        }

        return solutions;
    }

    private async Task<List<Component>> DiscoverComponentsAsync(
        Guid operationId,
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
                    },
                    PageInfo = new PagingInfo
                    {
                        Count = 5000,
                        PageNumber = 1,
                        PagingCookie = null
                    }
                };

                var totalProcessed = 0;
                EntityCollection results;

                do
                {
                    results = await Task.Run(() => _serviceClient.RetrieveMultiple(query), cancellationToken);
                    totalProcessed += results.Entities.Count;

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
                            // Placeholder values - will be resolved after collecting all components
                            LogicalName = objectId.ToString(),
                            DisplayName = $"{componentTypeName}"
                        };

                        components.Add(component);
                        // Components will be saved in batch after all solutions are processed
                    }

                    // Check if there are more pages
                    if (results.MoreRecords)
                    {
                        query.PageInfo.PageNumber++;
                        query.PageInfo.PagingCookie = results.PagingCookie;
                        _logger.LogDebug("Retrieving page {Page} for solution {Solution}, processed {Total} records", 
                            query.PageInfo.PageNumber, solution.UniqueName, totalProcessed);
                    }
                }
                while (results.MoreRecords);

                _logger.LogInformation("Processed {Total} solution components for {Solution}", totalProcessed, solution.UniqueName);
            }

            // Phase: Resolve component names
            await EmitProgressAsync(operationId, "names", 0, "Resolving component names...");
            var totalToResolve = components.Count;
            var resolved = 0;

            foreach (var component in components)
            {
                try
                {
                    var nameInfo = await _nameResolver.ResolveAsync(
                        component.ObjectId,
                        component.ComponentTypeCode,
                        cancellationToken);

                    component.LogicalName = nameInfo.LogicalName;
                    component.DisplayName = nameInfo.DisplayName;
                    component.TableLogicalName = nameInfo.TableLogicalName;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve name for component {ObjectId}", component.ObjectId);
                    // Keep the fallback values
                }

                resolved++;
                if (resolved % 50 == 0 || resolved == totalToResolve)
                {
                    var percent = totalToResolve > 0 ? resolved * 100 / totalToResolve : 100;
                    await EmitProgressAsync(operationId, "names", percent, 
                        $"Resolved {resolved}/{totalToResolve} component names");
                }
            }

            // Save all components in a single batch
            await using (var dbContext = new AnalyzerDbContext(_dbContextOptions))
            {
                dbContext.Components.AddRange(components);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            
            _logger.LogInformation("Discovered {Count} components", components.Count);
        }
        catch (SessionExpiredException ex)
        {
            _logger.LogError(ex, "Session timed out while discovering solutions");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering components");
        }

        return components;
    }

    private async Task<int> BuildLayerStacksAsync(
        Guid operationId,
        List<Component> components,
        CancellationToken cancellationToken)
    {
        var layerCount = 0;
        var allLayers = new List<Layer>();
        Dictionary<string, Solution> solutionCache;
        var totalComponents = components.Count;
        var processedComponents = 0;

        // Load all solutions into cache to avoid repeated database queries
        await using (var dbContext = new AnalyzerDbContext(_dbContextOptions))
        {
            solutionCache = await dbContext.Solutions.ToDictionaryAsync(s => s.UniqueName, cancellationToken);
        }

        try
        {
            foreach (var component in components)
            {
                var componentTypeName = GetDataverseComponentTypeName(component.ComponentType);
                
                // Emit granular progress for each component
                var percent = totalComponents > 0 ? processedComponents * 100 / totalComponents : 0;
                await EmitProgressAsync(operationId, "layers", percent, 
                    $"Processing component {processedComponents + 1}/{totalComponents}: {component.ComponentType} ({component.ObjectId})");
                
                // Query msdyn_componentlayer for this component
                var query = new QueryExpression("msdyn_componentlayer")
                {
                    ColumnSet = new ColumnSet("msdyn_componentlayerid", "msdyn_solutionname", "msdyn_order", "msdyn_publishername", "msdyn_componentjson", "msdyn_changes"),
                    Criteria = new FilterExpression(LogicalOperator.And)
                    {
                        Conditions =
                        {
                            new ConditionExpression("msdyn_componentid", ConditionOperator.Equal, component.ObjectId),
                            new ConditionExpression("msdyn_solutioncomponentname", ConditionOperator.Equal, componentTypeName)
                        }
                    },
                    Orders = { new OrderExpression("msdyn_order", OrderType.Ascending) },
                    PageInfo = new PagingInfo
                    {
                        Count = 5000,
                        PageNumber = 1,
                        PagingCookie = null
                    }
                };

                EntityCollection results;
                var componentLayerCount = 0;

                try
                {
                    do
                    {
                        results = await _serviceClient.RetrieveMultipleAsync(query, cancellationToken);
                        componentLayerCount += results.Entities.Count;

                        foreach (var entity in results.Entities)
                        {
                            // Use ordinal value directly from Dataverse (fixes bug where ordinals were manually incremented)
                            var ordinal = entity.GetAttributeValue<int>("msdyn_order");
                            var solutionName = entity.GetAttributeValue<string>("msdyn_solutionname") ?? "Unknown";
                            var componentJson = entity.GetAttributeValue<string>("msdyn_componentjson");
                            var changes = entity.GetAttributeValue<string>("msdyn_changes");
                            
                            var layer = new Layer
                            {
                                LayerId = Guid.NewGuid(),
                                ComponentId = component.ComponentId,
                                Ordinal = ordinal,
                                SolutionName = solutionName,
                                Publisher = entity.Contains("msdyn_publishername") ? 
                                    entity.GetAttributeValue<string>("msdyn_publishername") ?? "Unknown" : "Unknown",
                                IsManaged = true,
                                Version = "1.0.0.0",
                                CreatedOn = DateTimeOffset.UtcNow,
                                ComponentJson = componentJson,
                                Changes = changes
                            };

                            // Look up solution from cache
                            if (solutionCache.TryGetValue(solutionName, out var solution))
                            {
                                layer.SolutionId = solution.SolutionId;
                                layer.IsManaged = solution.IsManaged;
                                layer.Version = solution.Version;
                                layer.Publisher = solution.Publisher;
                            }

                            // Extract and format layer attributes
                            try
                            {
                                var extractedAttributes = _attributeExtractor.ExtractAttributes(layer.LayerId, componentJson, changes);
                                layer.Attributes = extractedAttributes;
                                
                                _logger.LogDebug("Extracted {Count} attributes for layer {LayerId}", 
                                    extractedAttributes.Count, layer.LayerId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to extract attributes for layer {LayerId}", layer.LayerId);
                            }

                            allLayers.Add(layer);
                            layerCount++;
                        }

                        // Check if there are more pages
                        if (results.MoreRecords)
                        {
                            query.PageInfo.PageNumber++;
                            query.PageInfo.PagingCookie = results.PagingCookie;
                            _logger.LogDebug("Retrieving page {Page} for component {ComponentId}, processed {Total} layers", 
                                query.PageInfo.PageNumber, component.ComponentId, componentLayerCount);
                        }
                    }
                    while (results.MoreRecords);

                    if (componentLayerCount > 0)
                    {
                        _logger.LogDebug("Found {Count} layers for component {ComponentId}", componentLayerCount, component.ComponentId);
                    }
                }
                catch (SessionExpiredException ex)
                {
                    _logger.LogError(ex, "Session timed out while discovering solutions");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "No layers found for component {ComponentId}", component.ComponentId);
                }
                finally
                {
                    processedComponents++;
                }
            }

            // Save all layers in a single batch
            await using (var dbContext = new AnalyzerDbContext(_dbContextOptions))
            {
                dbContext.Layers.AddRange(allLayers);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            
            _logger.LogInformation("Built {Count} layer records", layerCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building layer stacks");
        }

        return layerCount;
    }

    private async Task EmitProgressAsync(Guid operationId, string phase, int percent, string message)
    {
        var progressEvent = new PluginEvent
        {
            PluginId = "com.ddk.solutionlayeranalyzer",
            Type = "plugin:sla:progress",
            Payload = JsonSerializer.Serialize(new { operationId, phase, percent, message }, JsonOptions),
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

    /// <summary>
    /// Gets metadata about the current index including source and target solutions.
    /// </summary>
    public static async Task<IndexMetadataResponse> GetIndexMetadataAsync(
        DbContextOptions<AnalyzerDbContext> dbContextOptions,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AnalyzerDbContext(dbContextOptions);

        var solutionCount = await dbContext.Solutions.CountAsync(cancellationToken);
        if (solutionCount == 0)
        {
            return new IndexMetadataResponse { HasIndex = false };
        }

        var sourceSolutions = await dbContext.Solutions
            .Where(s => s.IsSource)
            .Select(s => s.UniqueName)
            .ToListAsync(cancellationToken);

        var targetSolutions = await dbContext.Solutions
            .Where(s => s.IsTarget)
            .Select(s => s.UniqueName)
            .ToListAsync(cancellationToken);

        var componentCount = await dbContext.Components.CountAsync(cancellationToken);
        var layerCount = await dbContext.Layers.CountAsync(cancellationToken);

        return new IndexMetadataResponse
        {
            HasIndex = true,
            SourceSolutions = sourceSolutions,
            TargetSolutions = targetSolutions,
            Stats = new IndexStats
            {
                Solutions = solutionCount,
                Components = componentCount,
                Layers = layerCount
            }
        };
    }
}
