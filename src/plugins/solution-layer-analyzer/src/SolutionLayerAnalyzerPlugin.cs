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

        // Get Dataverse client
        var dataverseClient = _context.GetDataverseClient(request.ConnectionId);

        // Create indexing service
        var indexingService = new IndexingService(
            _dbContext!,
            _context.Logger,
            dataverseClient,
            _context);

        // Execute indexing
        var response = await indexingService.IndexAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response);
    }

    private async Task<JsonElement> ExecuteQueryAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryRequest>(payload)
            ?? throw new ArgumentException("Invalid query request payload", nameof(payload));

        _context!.Logger.LogInformation("Executing query with {FilterCount} filters", request.Filters != null ? 1 : 0);

        // Query the database
        var query = _dbContext!.Components
            .Include(c => c.Layers)
            .AsQueryable();

        // Apply paging
        var total = await query.CountAsync(cancellationToken);
        var components = await query
            .Skip(request.Paging.Skip)
            .Take(request.Paging.Take)
            .ToListAsync(cancellationToken);

        // Map to response
        var rows = components.Select(c => new ComponentResult
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

        var response = new QueryResponse
        {
            Rows = rows,
            Total = total
        };

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

        // TODO: Implement actual diff logic with payload retrieval
        var response = new DiffResponse
        {
            LeftText = "// Left payload not yet implemented",
            RightText = "// Right payload not yet implemented",
            Mime = "text/plain",
            Warnings = new List<string> { "Diff functionality not yet fully implemented" }
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
