using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Services;

namespace Ddk.SolutionLayerAnalyzer;

/// <summary>
/// Solution Layer Analyzer plugin for analyzing Dataverse solution component layering.
/// </summary>
public sealed class SolutionLayerAnalyzerPlugin : IToolPlugin
{
    private IPluginContext? _context;
    private SqliteConnection? _keepAliveConnection;
    private DbContextOptions<AnalyzerDbContext>? _dbContextOptions;
    private string? _databaseName;
    private bool _disposed;
    
    /// <summary>
    /// Semaphore to serialize database operations. SQLite in-memory shared cache 
    /// does not handle concurrent operations well, so we serialize all DB access.
    /// </summary>
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    /// <summary>
    /// Finalizer to ensure cleanup if DisposeAsync is not called.
    /// </summary>
    ~SolutionLayerAnalyzerPlugin()
    {
        DisposeCore();
    }

    /// <inheritdoc/>
    public string PluginId => "com.ddk.solutionlayeranalyzer";

    /// <inheritdoc/>
    public string Name => "Solution Layer Analyzer";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <summary>
    /// Creates a new DbContext instance for thread-safe database access.
    /// Each operation should create its own DbContext to avoid concurrency issues.
    /// </summary>
    private AnalyzerDbContext CreateDbContext()
    {
        if (_dbContextOptions == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }
        return new AnalyzerDbContext(_dbContextOptions);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _context.Logger.LogInformation("Solution Layer Analyzer plugin initialized");

        // Use a unique database name per instance to avoid lock conflicts between runs
        _databaseName = $"SolutionLayerAnalyzer_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={_databaseName};Mode=Memory;Cache=Shared";

        // Create a shared connection that stays open to keep the in-memory database alive
        _keepAliveConnection = new SqliteConnection(connectionString);
        await _keepAliveConnection.OpenAsync(cancellationToken);

        // Configure DbContext options to use the shared in-memory database
        _dbContextOptions = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseSqlite(connectionString)
            .Options;

        // Create schema using a temporary context
        using (var initContext = new AnalyzerDbContext(_dbContextOptions))
        {
            await initContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        // Register for process exit to ensure cleanup on forced termination
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        
        _context.Logger.LogInformation("In-memory database initialized: {DatabaseName}", _databaseName);
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        // Synchronously dispose on process exit since we can't await
        DisposeCore();
    }

    private void DisposeCore()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try
        {
            // Unregister the event handler
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
        catch
        {
            // Ignore errors during shutdown
        }

        try
        {
            _keepAliveConnection?.Dispose();
            _keepAliveConnection = null;
        }
        catch
        {
            // Ignore errors during shutdown  
        }

        try
        {
            _dbLock.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PluginCommand>> GetCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commands = new List<PluginCommand>
        {
            new()
            {
                Name = "index",
                Label = "Index Solutions",
                Description = "Build an index of solutions, components, and their layers from Dataverse"
            },
            new()
            {
                Name = "query",
                Label = "Query Components",
                Description = "Query indexed components with advanced filtering and grouping"
            },
            new()
            {
                Name = "details",
                Label = "Get Component Details",
                Description = "Get full layer stack for a specific component"
            },
            new()
            {
                Name = "diff",
                Label = "Diff Component Layers",
                Description = "Compare payloads between two layers of a component"
            },
            new()
            {
                Name = "clear",
                Label = "Clear Index",
                Description = "Clear the in-memory index"
            },
            new()
            {
                Name = "fetchSolutions",
                Label = "Fetch Solutions",
                Description = "Fetch all available solutions from Dataverse"
            },
            new()
            {
                Name = "getComponentTypes",
                Label = "Get Component Types",
                Description = "Get all supported component types"
            }
        };

        return Task.FromResult<IReadOnlyList<PluginCommand>>(commands);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task<JsonElement> ExecuteAsync(string commandName, string payload, CancellationToken cancellationToken = default)
    {
        if (_context == null || _dbContextOptions == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        _context.Logger.LogInformation("Executing command: {Command}", commandName);

        return commandName switch
        {
            "index" => await ExecuteIndexAsync(payload, cancellationToken),
            "query" => await ExecuteQueryAsync(payload, cancellationToken),
            "details" => await ExecuteDetailsAsync(payload, cancellationToken),
            "diff" => await ExecuteDiffAsync(payload, cancellationToken),
            "clear" => await ExecuteClearAsync(cancellationToken),
            "fetchSolutions" => await ExecuteFetchSolutionsAsync(payload, cancellationToken),
            "getComponentTypes" => await ExecuteGetComponentTypesAsync(cancellationToken),
            "saveIndexConfig" => await ExecuteSaveIndexConfigAsync(payload, cancellationToken),
            "loadIndexConfigs" => await ExecuteLoadIndexConfigsAsync(payload, cancellationToken),
            "saveFilterConfig" => await ExecuteSaveFilterConfigAsync(payload, cancellationToken),
            "loadFilterConfigs" => await ExecuteLoadFilterConfigsAsync(payload, cancellationToken),
            _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
        };
    }

    private async Task<JsonElement> ExecuteIndexAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<IndexRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid index request payload", nameof(payload));

        if (_dbContextOptions == null)
        {
            throw new InvalidOperationException("Plugin not initialized");
        }

        _context!.Logger.LogInformation(
            "Starting indexing: {SourceCount} source solutions, {TargetCount} target solutions",
            request.SourceSolutions.Count,
            request.TargetSolutions.Count);

        // Get ServiceClient from factory
        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);

        // Serialize database access to avoid SQLite concurrency issues
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Create indexing service with DbContextOptions
            var indexingService = new IndexingService(
                _dbContextOptions,
                _context.Logger,
                serviceClient,
                _context);

            // Start indexing (non-blocking)
            var response = await indexingService.StartIndexAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteQueryAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid query request payload", nameof(payload));

        _context!.Logger.LogInformation("Executing query with filters: {HasFilters}", request.Filters != null);

        // Serialize database access to avoid SQLite concurrency issues
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Use QueryService with a new DbContext instance
            await using var dbContext = CreateDbContext();
            var queryService = new QueryService(dbContext);
            var response = await queryService.QueryAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteDetailsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DetailsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid details request payload", nameof(payload));

        _context!.Logger.LogInformation("Getting details for component: {ComponentId}", request.ComponentId);

        // Serialize database access to avoid SQLite concurrency issues
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var component = await dbContext.Components
                .Include(c => c.Layers)
                .FirstOrDefaultAsync(c => c.ComponentId == request.ComponentId, cancellationToken);

            if (component == null)
            {
                throw new InvalidOperationException($"Component not found: {request.ComponentId}");
            }

            var response = new DetailsResponse
            {
                Layers = component.Layers
                    .OrderBy(l => l.Ordinal)
                    .Select(l => new LayerDetail
                    {
                        SolutionName = l.SolutionName,
                        Publisher = l.Publisher,
                        IsManaged = l.IsManaged,
                        Version = l.Version,
                        CreatedOn = l.CreatedOn,
                        Ordinal = l.Ordinal
                    })
                    .ToList()
            };

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteDiffAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DiffRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid diff request payload", nameof(payload));

        _context!.Logger.LogInformation(
            "Diffing component {ComponentId} between {Left} and {Right}",
            request.ComponentId,
            request.Left.SolutionName,
            request.Right.SolutionName);

        // Get component data within lock, then release for network calls
        Models.Component component;
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var found = await dbContext.Components
                .Include(c => c.Layers)
                .FirstOrDefaultAsync(c => c.ComponentId == request.ComponentId, cancellationToken);

            if (found == null)
            {
                throw new InvalidOperationException($"Component not found: {request.ComponentId}");
            }
            component = found;
        }
        finally
        {
            _dbLock.Release();
        }

        // Get ServiceClient for payload retrieval (outside lock for long-running network calls)
        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);
        var payloadService = new PayloadService(serviceClient, _context.Logger);

        // Find left and right layers
        var leftLayer = component.Layers.FirstOrDefault(l => l.SolutionName == request.Left.SolutionName);
        var rightLayer = component.Layers.FirstOrDefault(l => l.SolutionName == request.Right.SolutionName);

        // Retrieve payloads from componentjson (works for ALL component types)
        string? leftText = null;
        string? rightText = null;
        string? leftMime = "application/json";
        string? rightMime = "application/json";

        if (leftLayer?.ComponentJson != null)
        {
            var (leftPayload, leftPayloadMime) = payloadService.RetrievePayloadFromComponentJson(leftLayer.ComponentJson);
            leftText = leftPayload;
            leftMime = leftPayloadMime;
        }

        if (rightLayer?.ComponentJson != null)
        {
            var (rightPayload, rightPayloadMime) = payloadService.RetrievePayloadFromComponentJson(rightLayer.ComponentJson);
            rightText = rightPayload;
            rightMime = rightPayloadMime;
        }

        var warnings = new List<string>();
        if (leftText == null)
        {
            warnings.Add($"Could not retrieve payload for left solution: {request.Left.SolutionName}");
            leftText = "// Payload not available";
        }
        if (rightText == null)
        {
            warnings.Add($"Could not retrieve payload for right solution: {request.Right.SolutionName}");
            rightText = "// Payload not available";
        }

        var response = new DiffResponse
        {
            LeftText = leftText ?? string.Empty,
            RightText = rightText ?? string.Empty,
            Mime = leftMime ?? rightMime ?? "application/json",
            Warnings = warnings
        };

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteClearAsync(CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("Clearing index");

        // Serialize database access to avoid SQLite concurrency issues
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            // Clear all tables using a new DbContext instance
            await using var dbContext = CreateDbContext();
            dbContext.Artifacts.RemoveRange(dbContext.Artifacts);
            dbContext.Layers.RemoveRange(dbContext.Layers);
            dbContext.Components.RemoveRange(dbContext.Components);
            dbContext.Solutions.RemoveRange(dbContext.Solutions);

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new { Success = true, Message = "Index cleared successfully" };
            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteSaveIndexConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SaveIndexConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid saveIndexConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Saving index config: {Name}", request.Name);

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var configService = new ConfigService(dbContext, _context.Logger);
            var response = await configService.SaveIndexConfigAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteLoadIndexConfigsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LoadIndexConfigsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid loadIndexConfigs request payload", nameof(payload));

        _context!.Logger.LogInformation("Loading index configs");

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var configService = new ConfigService(dbContext, _context.Logger);
            var response = await configService.LoadIndexConfigsAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteSaveFilterConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SaveFilterConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid saveFilterConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Saving filter config: {Name}", request.Name);

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var configService = new ConfigService(dbContext, _context.Logger);
            var response = await configService.SaveFilterConfigAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteLoadFilterConfigsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LoadFilterConfigsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid loadFilterConfigs request payload", nameof(payload));

        _context!.Logger.LogInformation("Loading filter configs");

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = CreateDbContext();
            var configService = new ConfigService(dbContext, _context.Logger);
            var response = await configService.LoadFilterConfigsAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteFetchSolutionsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<FetchSolutionsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid fetchSolutions request payload", nameof(payload));

        _context!.Logger.LogInformation("Fetching solutions from Dataverse");

        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("solution")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "friendlyname", "version", "ismanaged", "publisherid"),
            Criteria = new Microsoft.Xrm.Sdk.Query.FilterExpression(Microsoft.Xrm.Sdk.Query.LogicalOperator.And)
        };
        
        // Exclude default/internal solutions
        query.Criteria.AddCondition("isvisible", Microsoft.Xrm.Sdk.Query.ConditionOperator.Equal, true);
        query.Criteria.AddCondition("ismanaged", Microsoft.Xrm.Sdk.Query.ConditionOperator.In, true, false);

        var results = await Task.Run(() => serviceClient.RetrieveMultiple(query), cancellationToken);

        var solutions = new List<SolutionInfo>();
        foreach (var entity in results.Entities)
        {
            solutions.Add(new SolutionInfo
            {
                UniqueName = entity.GetAttributeValue<string>("uniquename") ?? string.Empty,
                DisplayName = entity.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                Version = entity.GetAttributeValue<string>("version") ?? "1.0.0.0",
                IsManaged = entity.GetAttributeValue<bool>("ismanaged"),
                Publisher = entity.Contains("publisherid") 
                    ? ((Microsoft.Xrm.Sdk.EntityReference)entity["publisherid"]).Name 
                    : null
            });
        }

        var response = new FetchSolutionsResponse
        {
            Solutions = solutions.OrderBy(s => s.DisplayName).ToList()
        };

        _context.Logger.LogInformation("Fetched {Count} solutions", solutions.Count);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private Task<JsonElement> ExecuteGetComponentTypesAsync(CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("Getting component types");

        var componentTypes = new List<ComponentTypeInfo>
        {
            new() { Name = "Entity", DisplayName = "Entity (Table)", TypeCode = ComponentTypeCodes.Entity },
            new() { Name = "Attribute", DisplayName = "Attribute (Column)", TypeCode = ComponentTypeCodes.Attribute },
            new() { Name = "SystemForm", DisplayName = "Form", TypeCode = ComponentTypeCodes.SystemForm },
            new() { Name = "SavedQuery", DisplayName = "View", TypeCode = ComponentTypeCodes.SavedQuery },
            new() { Name = "SavedQueryVisualization", DisplayName = "Chart", TypeCode = ComponentTypeCodes.SavedQueryVisualization },
            new() { Name = "RibbonCustomization", DisplayName = "Ribbon", TypeCode = ComponentTypeCodes.RibbonCustomization },
            new() { Name = "WebResource", DisplayName = "Web Resource", TypeCode = ComponentTypeCodes.WebResource },
            new() { Name = "SDKMessageProcessingStep", DisplayName = "Plugin Step", TypeCode = ComponentTypeCodes.SDKMessageProcessingStep },
            new() { Name = "Workflow", DisplayName = "Workflow/Business Process Flow", TypeCode = ComponentTypeCodes.Workflow },
            new() { Name = "AppModule", DisplayName = "Model-Driven App", TypeCode = ComponentTypeCodes.AppModule },
            new() { Name = "SiteMap", DisplayName = "Sitemap", TypeCode = ComponentTypeCodes.SiteMap },
            new() { Name = "OptionSet", DisplayName = "Option Set (Choice)", TypeCode = ComponentTypeCodes.OptionSet },
            new() { Name = "Relationship", DisplayName = "Relationship", TypeCode = ComponentTypeCodes.Relationship },
            new() { Name = "Report", DisplayName = "Report", TypeCode = ComponentTypeCodes.Report },
            new() { Name = "EmailTemplate", DisplayName = "Email Template", TypeCode = ComponentTypeCodes.EmailTemplate },
            new() { Name = "CustomControl", DisplayName = "Custom Control", TypeCode = ComponentTypeCodes.CustomControl },
            new() { Name = "CustomAPI", DisplayName = "Custom API", TypeCode = ComponentTypeCodes.CustomAPI },
            new() { Name = "ConnectionRole", DisplayName = "Connection Role", TypeCode = ComponentTypeCodes.ConnectionRole },
            new() { Name = "ServiceEndpoint", DisplayName = "Service Endpoint", TypeCode = ComponentTypeCodes.ServiceEndpoint },
            new() { Name = "PluginPackage", DisplayName = "Plugin Package", TypeCode = ComponentTypeCodes.PluginPackage },
        };

        var response = new GetComponentTypesResponse
        {
            ComponentTypes = componentTypes
        };

        return Task.FromResult(JsonSerializer.SerializeToElement(response, JsonOptions));
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        _context?.Logger.LogInformation("Solution Layer Analyzer plugin disposed");
        DisposeCore();
        GC.SuppressFinalize(this);
        return Task.CompletedTask;
    }
}
