using System.Text.Json;
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
    private AnalyzerDbContext? _dbContext;

    /// <inheritdoc/>
    public string PluginId => "com.ddk.solutionlayeranalyzer";

    /// <inheritdoc/>
    public string Name => "Solution Layer Analyzer";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    public async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _context.Logger.LogInformation("Solution Layer Analyzer plugin initialized");

        // Initialize in-memory database
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new AnalyzerDbContext(options);
        
        // Ensure database is created
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
        
        _context.Logger.LogInformation("In-memory database initialized");
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
            }
        };

        return Task.FromResult<IReadOnlyList<PluginCommand>>(commands);
    }

    /// <inheritdoc/>
    public async Task<JsonElement> ExecuteAsync(string commandName, string payload, CancellationToken cancellationToken = default)
    {
        if (_context == null || _dbContext == null)
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
            _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
        };
    }

    private async Task<JsonElement> ExecuteIndexAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<IndexRequest>(payload)
            ?? throw new ArgumentException("Invalid index request payload", nameof(payload));

        _context!.Logger.LogInformation(
            "Starting indexing: {SourceCount} source solutions, {TargetCount} target solutions",
            request.SourceSolutions.Count,
            request.TargetSolutions.Count);

        // Get ServiceClient from factory
        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);

        // Create indexing service
        var indexingService = new IndexingService(
            _dbContext!,
            _context.Logger,
            serviceClient,
            _context);

        // Execute indexing
        var response = await indexingService.IndexAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement> ExecuteQueryAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryRequest>(payload)
            ?? throw new ArgumentException("Invalid query request payload", nameof(payload));

        _context!.Logger.LogInformation("Executing query with filters: {HasFilters}", request.Filters != null);

        // Use QueryService with filter evaluation
        var queryService = new QueryService(_dbContext!);
        var response = await queryService.QueryAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement> ExecuteDetailsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DetailsRequest>(payload)
            ?? throw new ArgumentException("Invalid details request payload", nameof(payload));

        _context!.Logger.LogInformation("Getting details for component: {ComponentId}", request.ComponentId);

        var component = await _dbContext!.Components
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

        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement> ExecuteDiffAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DiffRequest>(payload)
            ?? throw new ArgumentException("Invalid diff request payload", nameof(payload));

        _context!.Logger.LogInformation(
            "Diffing component {ComponentId} between {Left} and {Right}",
            request.ComponentId,
            request.Left.SolutionName,
            request.Right.SolutionName);

        // Get the component
        var component = await _dbContext!.Components
            .Include(c => c.Layers)
            .FirstOrDefaultAsync(c => c.ComponentId == request.ComponentId, cancellationToken);

        if (component == null)
        {
            throw new InvalidOperationException($"Component not found: {request.ComponentId}");
        }

        // Get ServiceClient for payload retrieval
        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);
        var payloadService = new PayloadService(serviceClient, _context.Logger);

        // Retrieve left payload
        var (leftText, leftMime) = await payloadService.RetrievePayloadAsync(
            component.ObjectId,
            component.ComponentType,
            request.Left.SolutionName,
            cancellationToken);

        // Retrieve right payload
        var (rightText, rightMime) = await payloadService.RetrievePayloadAsync(
            component.ObjectId,
            component.ComponentType,
            request.Right.SolutionName,
            cancellationToken);

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
            Mime = leftMime ?? rightMime ?? "text/plain",
            Warnings = warnings
        };

        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement> ExecuteClearAsync(CancellationToken cancellationToken)
    {
        _context!.Logger.LogInformation("Clearing index");

        // Clear all tables
        _dbContext!.Artifacts.RemoveRange(_dbContext.Artifacts);
        _dbContext.Layers.RemoveRange(_dbContext.Layers);
        _dbContext.Components.RemoveRange(_dbContext.Components);
        _dbContext.Solutions.RemoveRange(_dbContext.Solutions);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new { Success = true, Message = "Index cleared successfully" };
        return JsonSerializer.SerializeToElement(response);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        _context?.Logger.LogInformation("Solution Layer Analyzer plugin disposed");
        
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
    }
}
