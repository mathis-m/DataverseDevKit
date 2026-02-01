using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DataverseDevKit.Core.Abstractions;
using DataverseDevKit.Core.Models;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Models;
using Ddk.SolutionLayerAnalyzer.Services;
using Ddk.SolutionLayerAnalyzer.Filters;

namespace Ddk.SolutionLayerAnalyzer;

/// <summary>
/// Solution Layer Analyzer plugin for analyzing Dataverse solution component layering.
/// </summary>
public sealed class SolutionLayerAnalyzerPlugin : IToolPlugin
{
    private IPluginContext? _context;
    private bool _disposed;

    /// <summary>
    /// Cache of DbContextOptions per connection ID.
    /// Each connection gets its own SQLite database file on disk.
    /// </summary>
    private readonly Dictionary<string, DbContextOptions<AnalyzerDbContext>> _dbOptionsCache = new();

    /// <summary>
    /// Lock for thread-safe access to the options cache.
    /// </summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// Semaphore to serialize database operations per connection.
    /// </summary>
    private readonly Dictionary<string, SemaphoreSlim> _dbLocks = new();

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
    /// Gets the database directory path.
    /// </summary>
    private static string GetDatabaseDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "DataverseDevKit", "SolutionLayerAnalyzer");
    }

    /// <summary>
    /// Gets or creates DbContextOptions for a specific connection ID.
    /// </summary>
    private DbContextOptions<AnalyzerDbContext> GetOrCreateDbOptions(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException("Connection ID is required", nameof(connectionId));
        }

        lock (_cacheLock)
        {
            if (_dbOptionsCache.TryGetValue(connectionId, out var cached))
            {
                return cached;
            }

            // Create database file path unique to this connection
            var dbDirectory = GetDatabaseDirectory();
            Directory.CreateDirectory(dbDirectory);

            // Sanitize connection ID for use in filename
            var safeConnectionId = string.Join("_", connectionId.Split(Path.GetInvalidFileNameChars()));
            var dbPath = Path.Combine(dbDirectory, $"analyzer_{safeConnectionId}.db");
            var connectionString = $"Data Source={dbPath}";

            var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
                .UseSqlite(connectionString)
                .Options;

            // Ensure schema exists
            using (var initContext = new AnalyzerDbContext(options))
            {
                initContext.Database.EnsureCreated();
            }

            _dbOptionsCache[connectionId] = options;
            _context?.Logger.LogInformation("Initialized database for connection {ConnectionId}: {DbPath}", connectionId, dbPath);

            return options;
        }
    }

    /// <summary>
    /// Gets the semaphore for a specific connection ID.
    /// </summary>
    private SemaphoreSlim GetDbLock(string connectionId)
    {
        lock (_cacheLock)
        {
            if (!_dbLocks.TryGetValue(connectionId, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _dbLocks[connectionId] = semaphore;
            }
            return semaphore;
        }
    }

    /// <summary>
    /// Creates a new DbContext instance for a specific connection.
    /// </summary>
    private AnalyzerDbContext CreateDbContext(string connectionId)
    {
        var options = GetOrCreateDbOptions(connectionId);
        return new AnalyzerDbContext(options);
    }

    /// <summary>
    /// Parses and normalizes a component JSON string into a JsonElement.
    /// Recursively expands nested JSON strings within attribute values.
    /// </summary>
    /// <param name="componentJson">The raw component JSON string.</param>
    /// <returns>The parsed and normalized JsonElement, or null if the string is null/empty or parsing fails.</returns>
    private static JsonElement? ParseComponentJson(string? componentJson)
    {
        if (string.IsNullOrWhiteSpace(componentJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(componentJson);
            var normalized = NormalizeJsonElement(doc.RootElement);
            return JsonSerializer.SerializeToElement(normalized, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Recursively normalizes a JsonElement, detecting and parsing nested JSON strings.
    /// </summary>
    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeJsonObject(element),
            JsonValueKind.Array => NormalizeJsonArray(element),
            JsonValueKind.String => NormalizeJsonString(element.GetString()),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Normalizes a JSON object by recursively normalizing its properties.
    /// </summary>
    private static Dictionary<string, object?> NormalizeJsonObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = NormalizeJsonElement(property.Value);
        }
        return result;
    }

    /// <summary>
    /// Normalizes a JSON array by recursively normalizing its elements.
    /// </summary>
    private static List<object?> NormalizeJsonArray(JsonElement element)
    {
        var result = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            result.Add(NormalizeJsonElement(item));
        }
        return result;
    }

    /// <summary>
    /// Normalizes a string value, attempting to parse it as nested JSON if possible.
    /// This handles double-encoded JSON strings from Dataverse componentjson.
    /// </summary>
    private static object? NormalizeJsonString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();

        // Check if the string looks like it could be JSON (object or array)
        if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
            (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
        {
            try
            {
                using var nestedDoc = JsonDocument.Parse(trimmed);
                // Successfully parsed as JSON - recursively normalize it
                return NormalizeJsonElement(nestedDoc.RootElement);
            }
            catch (JsonException)
            {
                // Not valid JSON, return as string
                return value;
            }
        }

        return value;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _context.Logger.LogInformation("Solution Layer Analyzer plugin initialized");
        _context.Logger.LogInformation("Database directory: {DbDirectory}", GetDatabaseDirectory());

        return Task.CompletedTask;
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
            lock (_cacheLock)
            {
                foreach (var semaphore in _dbLocks.Values)
                {
                    semaphore.Dispose();
                }
                _dbLocks.Clear();
                _dbOptionsCache.Clear();
            }
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
                Description = "Clear the index for a specific connection"
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
            },
            new()
            {
                Name = "getAnalytics",
                Label = "Get Analytics",
                Description = "Get comprehensive analytics including risk scores, violations, and graph data"
            },
            new()
            {
                Name = "saveReport",
                Label = "Save Report",
                Description = "Save a query/filter as a report configuration"
            },
            new()
            {
                Name = "updateReport",
                Label = "Update Report",
                Description = "Update an existing report configuration"
            },
            new()
            {
                Name = "deleteReport",
                Label = "Delete Report",
                Description = "Delete a report"
            },
            new()
            {
                Name = "duplicateReport",
                Label = "Duplicate Report",
                Description = "Create a copy of an existing report"
            },
            new()
            {
                Name = "listReports",
                Label = "List Reports",
                Description = "List all reports organized by groups"
            },
            new()
            {
                Name = "executeReport",
                Label = "Execute Report",
                Description = "Execute a saved report and get results"
            },
            new()
            {
                Name = "reorderReports",
                Label = "Reorder Reports",
                Description = "Reorder reports and change their grouping"
            },
            new()
            {
                Name = "createReportGroup",
                Label = "Create Report Group",
                Description = "Create a new report group"
            },
            new()
            {
                Name = "updateReportGroup",
                Label = "Update Report Group",
                Description = "Update a report group"
            },
            new()
            {
                Name = "deleteReportGroup",
                Label = "Delete Report Group",
                Description = "Delete a report group"
            },
            new()
            {
                Name = "reorderReportGroups",
                Label = "Reorder Report Groups",
                Description = "Reorder report groups"
            },
            new()
            {
                Name = "exportConfig",
                Label = "Export Configuration",
                Description = "Export analyzer configuration to YAML file"
            },
            new()
            {
                Name = "importConfig",
                Label = "Import Configuration",
                Description = "Import analyzer configuration from YAML file"
            },
            new()
            {
                Name = "generateReportOutput",
                Label = "Generate Report Output",
                Description = "Generate detailed report output in YAML or JSON format"
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
        if (_context == null)
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
            "clear" => await ExecuteClearAsync(payload, cancellationToken),
            "fetchSolutions" => await ExecuteFetchSolutionsAsync(payload, cancellationToken),
            "getComponentTypes" => await ExecuteGetComponentTypesAsync(cancellationToken),
            "getAnalytics" => await ExecuteGetAnalyticsAsync(payload, cancellationToken),
            "getIndexMetadata" => await ExecuteGetIndexMetadataAsync(payload, cancellationToken),
            "saveIndexConfig" => await ExecuteSaveIndexConfigAsync(payload, cancellationToken),
            "loadIndexConfigs" => await ExecuteLoadIndexConfigsAsync(payload, cancellationToken),
            "saveFilterConfig" => await ExecuteSaveFilterConfigAsync(payload, cancellationToken),
            "loadFilterConfigs" => await ExecuteLoadFilterConfigsAsync(payload, cancellationToken),
            "saveReport" => await ExecuteSaveReportAsync(payload, cancellationToken),
            "updateReport" => await ExecuteUpdateReportAsync(payload, cancellationToken),
            "deleteReport" => await ExecuteDeleteReportAsync(payload, cancellationToken),
            "duplicateReport" => await ExecuteDuplicateReportAsync(payload, cancellationToken),
            "listReports" => await ExecuteListReportsAsync(payload, cancellationToken),
            "executeReport" => await ExecuteExecuteReportAsync(payload, cancellationToken),
            "reorderReports" => await ExecuteReorderReportsAsync(payload, cancellationToken),
            "createReportGroup" => await ExecuteCreateReportGroupAsync(payload, cancellationToken),
            "updateReportGroup" => await ExecuteUpdateReportGroupAsync(payload, cancellationToken),
            "deleteReportGroup" => await ExecuteDeleteReportGroupAsync(payload, cancellationToken),
            "reorderReportGroups" => await ExecuteReorderReportGroupsAsync(payload, cancellationToken),
            "exportConfig" => await ExecuteExportConfigAsync(payload, cancellationToken),
            "importConfig" => await ExecuteImportConfigAsync(payload, cancellationToken),
            "generateReportOutput" => await ExecuteGenerateReportOutputAsync(payload, cancellationToken),
            _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
        };
    }

    private async Task<JsonElement> ExecuteIndexAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<IndexRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid index request payload", nameof(payload));

        _context!.Logger.LogInformation(
            "Starting indexing: {SourceCount} source solutions, {TargetCount} target solutions",
            request.SourceSolutions.Count,
            request.TargetSolutions.Count);

        // Get ServiceClient from factory
        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);
        var dbOptions = GetOrCreateDbOptions(request.ConnectionId);
        var dbLock = GetDbLock(request.ConnectionId);

        // Serialize database access to avoid SQLite concurrency issues
        await dbLock.WaitAsync(cancellationToken);
        try
        {
            // Create indexing service with DbContextOptions
            var indexingService = new IndexingService(
                dbOptions,
                _context.Logger,
                serviceClient,
                _context);

            // Start indexing (non-blocking)
            var response = await indexingService.StartIndexAsync(request, cancellationToken);

            return JsonSerializer.SerializeToElement(response, JsonOptions);
        }
        finally
        {
            dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteQueryAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<QueryRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid query request payload", nameof(payload));

        var queryId = request.QueryId ?? Guid.NewGuid().ToString("N");
        _context!.Logger.LogInformation("Executing query {QueryId} with filters: {HasFilters}", queryId, request.Filters != null);

        // If event-based response is requested, run query in background and emit result
        if (request.UseEventResponse)
        {
            // Return acknowledgment immediately
            var ack = new QueryAcknowledgment
            {
                QueryId = queryId,
                Started = true
            };

            // Start query in background (fire-and-forget with proper error handling)
            _ = ExecuteQueryAndEmitResultAsync(request with { QueryId = queryId }, cancellationToken);

            return JsonSerializer.SerializeToElement(ack, JsonOptions);
        }

        // Synchronous response (backward compatible)
        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var response = await queryService.QueryAsync(request with { QueryId = queryId }, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task ExecuteQueryAndEmitResultAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        QueryResultEvent resultEvent;

        try
        {
            await using var dbContext = CreateDbContext(request.ConnectionId);
            var queryService = new QueryService(dbContext);
            var response = await queryService.QueryAsync(request, cancellationToken);

            resultEvent = new QueryResultEvent
            {
                QueryId = request.QueryId ?? string.Empty,
                Success = true,
                Rows = response.Rows,
                Total = response.Total,
                Stats = response.Stats
            };

            _context!.Logger.LogInformation(
                "Query {QueryId} completed: {Total} results",
                request.QueryId,
                response.Total);
        }
        catch (Exception ex)
        {
            _context!.Logger.LogError(ex, "Query {QueryId} failed", request.QueryId);

            resultEvent = new QueryResultEvent
            {
                QueryId = request.QueryId ?? string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };
        }

        // Emit the result event
        try
        {
            var pluginEvent = new PluginEvent
            {
                PluginId = PluginId,
                Type = "plugin:sla:query-result",
                Payload = JsonSerializer.Serialize(resultEvent, JsonOptions),
                Timestamp = DateTimeOffset.UtcNow
            };

            _context!.EmitEvent(pluginEvent);
            _context!.Logger.LogDebug("Emitted query result event for {QueryId}", request.QueryId);
        }
        catch (Exception ex)
        {
            _context!.Logger.LogError(ex, "Failed to emit query result event for {QueryId}", request.QueryId);
        }
    }

    private async Task<JsonElement> ExecuteDetailsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DetailsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid details request payload", nameof(payload));

        _context!.Logger.LogInformation("Getting details for component: {ComponentId}", request.ComponentId);


        await using var dbContext = CreateDbContext(request.ConnectionId);
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
                    Ordinal = l.Ordinal,
                    ComponentJson = ParseComponentJson(l.ComponentJson),
                    Changes = null // msdyn_changes would need to be retrieved separately if needed
                })
                .ToList()
        };

        return JsonSerializer.SerializeToElement(response, JsonOptions);
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

        var connectionId = request.ConnectionId ?? string.Empty;

        await using var dbContext = CreateDbContext(connectionId);

        // Verify component exists (lightweight query without joins)
        var componentExists = await dbContext.Components
            .AnyAsync(c => c.ComponentId == request.ComponentId, cancellationToken);

        if (!componentExists)
        {
            throw new InvalidOperationException($"Component not found: {request.ComponentId}");
        }

        // Query only the two specific layers by component ID and solution names
        // This avoids loading all layers for the component
        var solutionNames = new[] { request.Left.SolutionName, request.Right.SolutionName };
        var layers = await dbContext.Layers
            .Where(l => l.ComponentId == request.ComponentId && solutionNames.Contains(l.SolutionName))
            .ToListAsync(cancellationToken);

        // Find left and right layers from the filtered result
        var leftLayer = layers.FirstOrDefault(l => l.SolutionName == request.Left.SolutionName);
        var rightLayer = layers.FirstOrDefault(l => l.SolutionName == request.Right.SolutionName);

        // Load attributes only for the filtered layers (avoid loading attributes for all layers)
        if (leftLayer != null || rightLayer != null)
        {
            var layerIds = layers.Select(l => l.LayerId).ToList();
            var attributes = await dbContext.Set<LayerAttribute>()
                .Where(a => layerIds.Contains(a.LayerId))
                .ToListAsync(cancellationToken);

            // Associate attributes with their layers
            var attributesByLayerId = attributes.ToLookup(a => a.LayerId);
            foreach (var layer in layers)
            {
                layer.Attributes = attributesByLayerId[layer.LayerId].ToList();
            }
        }

        var warnings = new List<string>();

        if (leftLayer == null)
        {
            warnings.Add($"Left solution layer not found: {request.Left.SolutionName}");
        }

        if (rightLayer == null)
        {
            warnings.Add($"Right solution layer not found: {request.Right.SolutionName}");
        }

        // Build attribute diffs
        var attributeDiffs = new List<AttributeDiff>();

        if (leftLayer != null && rightLayer != null)
        {
            var leftAttributes = leftLayer.Attributes.ToList();
            var rightAttributes = rightLayer.Attributes.ToList();

            // Get all changed attributes from right layer
            var changedRightAttributes = rightAttributes.Where(a => a.IsChanged).ToList();

            if (changedRightAttributes.Count == 0)
            {
                warnings.Add("No changed attributes detected in right layer. This may indicate the layer has no changes tracked.");
            }
            else
            {
                // Build diffs for changed attributes
                foreach (var changedAttr in changedRightAttributes.Where(a => !FilterConstants.ExcludedAttributeNames.Contains(a.AttributeName)))
                {
                    var leftAttr = leftAttributes.FirstOrDefault(a =>
                        string.Equals(a.AttributeName, changedAttr.AttributeName, StringComparison.OrdinalIgnoreCase));

                    var leftValue = leftAttr?.AttributeValue;
                    var rightValue = changedAttr.AttributeValue;

                    var isDifferent = leftValue != rightValue;
                    var onlyInLeft = false; // changedAttr comes from right, so it can't be only in left
                    var onlyInRight = leftAttr == null;

                    // Only include if there's actually a difference
                    attributeDiffs.Add(new AttributeDiff
                    {
                        AttributeName = changedAttr.AttributeName,
                        LeftValue = leftValue,
                        RightValue = rightValue,
                        AttributeType = (int)changedAttr.AttributeType,
                        IsComplex = changedAttr.IsComplexValue || (leftAttr?.IsComplexValue ?? false),
                        OnlyInLeft = onlyInLeft,
                        OnlyInRight = onlyInRight,
                        IsDifferent = isDifferent
                    });
                }
                
                // Also check for attributes that exist in left but not in right (removed attributes)
                var rightAttributeNames = new HashSet<string>(
                    rightAttributes.Select(a => a.AttributeName), 
                    StringComparer.OrdinalIgnoreCase);
                
                foreach (var leftAttr in leftAttributes.Where(a => 
                    !FilterConstants.ExcludedAttributeNames.Contains(a.AttributeName) && 
                    !rightAttributeNames.Contains(a.AttributeName)))
                {
                    attributeDiffs.Add(new AttributeDiff
                    {
                        AttributeName = leftAttr.AttributeName,
                        LeftValue = leftAttr.AttributeValue,
                        RightValue = null,
                        AttributeType = (int)leftAttr.AttributeType,
                        IsComplex = leftAttr.IsComplexValue,
                        OnlyInLeft = true,
                        OnlyInRight = false,
                        IsDifferent = false
                    });
                }
            }
        }

        var response = new DiffResponse
        {
            Attributes = attributeDiffs,
            Warnings = warnings
        };

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteClearAsync(string payload, CancellationToken cancellationToken)
    {
        // Parse connection ID from payload
        var request = JsonSerializer.Deserialize<JsonElement>(payload);
        var connectionId = request.TryGetProperty("connectionId", out var connIdProp)
            ? connIdProp.GetString() ?? string.Empty
            : string.Empty;

        _context!.Logger.LogInformation("Clearing index for connection: {ConnectionId}", connectionId);

        var dbLock = GetDbLock(connectionId);

        // Serialize database access to avoid SQLite concurrency issues
        await dbLock.WaitAsync(cancellationToken);
        try
        {
            // Clear all tables using a new DbContext instance
            await using var dbContext = CreateDbContext(connectionId);
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
            dbLock.Release();
        }
    }

    private async Task<JsonElement> ExecuteSaveIndexConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SaveIndexConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid saveIndexConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Saving index config: {Name}", request.Name);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var configService = new ConfigService(dbContext, _context.Logger);
        var response = await configService.SaveIndexConfigAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteLoadIndexConfigsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LoadIndexConfigsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid loadIndexConfigs request payload", nameof(payload));

        _context!.Logger.LogInformation("Loading index configs");

        var connectionId = request.ConnectionId ?? string.Empty;
        await using var dbContext = CreateDbContext(connectionId);
        var configService = new ConfigService(dbContext, _context.Logger);
        var response = await configService.LoadIndexConfigsAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteSaveFilterConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SaveFilterConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid saveFilterConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Saving filter config: {Name}", request.Name);

        var connectionId = request.ConnectionId ?? string.Empty;
        await using var dbContext = CreateDbContext(connectionId);
        var configService = new ConfigService(dbContext, _context.Logger);
        var response = await configService.SaveFilterConfigAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteLoadFilterConfigsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<LoadFilterConfigsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid loadFilterConfigs request payload", nameof(payload));

        _context!.Logger.LogInformation("Loading filter configs");

        var connectionId = request.ConnectionId ?? string.Empty;
        await using var dbContext = CreateDbContext(connectionId);
        var configService = new ConfigService(dbContext, _context.Logger);
        var response = await configService.LoadFilterConfigsAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteFetchSolutionsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<FetchSolutionsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid fetchSolutions request payload", nameof(payload));

        if (string.IsNullOrEmpty(request.ConnectionId))
        {
            throw new ArgumentException("ConnectionId is required", nameof(payload));
        }

        _context!.Logger.LogInformation("Fetching solutions from Dataverse");

        var serviceClient = _context.ServiceClientFactory.GetServiceClient(request.ConnectionId);

        var query = new Microsoft.Xrm.Sdk.Query.QueryExpression("solution")
        {
            ColumnSet = new Microsoft.Xrm.Sdk.Query.ColumnSet("uniquename", "friendlyname", "version", "ismanaged", "publisherid")
        };

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

    private async Task<JsonElement> ExecuteGetIndexMetadataAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<IndexMetadataRequest>(payload, JsonOptions)
            ?? new IndexMetadataRequest();

        _context!.Logger.LogInformation("Getting index metadata for connection: {ConnectionId}", request.ConnectionId);

        var dbOptions = GetOrCreateDbOptions(request.ConnectionId);
        var response = await IndexingService.GetIndexMetadataAsync(dbOptions, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteGetAnalyticsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<GetAnalyticsRequest>(payload, JsonOptions)
            ?? new GetAnalyticsRequest();

        _context!.Logger.LogInformation("Computing analytics");

        var connectionId = request.ConnectionId ?? string.Empty;
        using var dbContext = CreateDbContext(connectionId);

        var analyticsService = new AnalyticsService(_context.Logger);
        var analytics = await analyticsService.ComputeAnalyticsAsync(dbContext, cancellationToken);

        _context.Logger.LogInformation(
            "Analytics computed: {Solutions} solutions, {Components} components with risks, {Violations} violations detected",
            analytics.SolutionMetrics.Count,
            analytics.ComponentRisks.Count,
            analytics.Violations.Count);

        return JsonSerializer.SerializeToElement(analytics, JsonOptions);
    }

    private async Task<JsonElement> ExecuteSaveReportAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<SaveReportRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid saveReport request payload", nameof(payload));

        _context!.Logger.LogInformation("Saving report: {Name}", request.Name);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.SaveReportAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteUpdateReportAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<UpdateReportRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid updateReport request payload", nameof(payload));

        _context!.Logger.LogInformation("Updating report: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.UpdateReportAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteDeleteReportAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DeleteReportRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid deleteReport request payload", nameof(payload));

        _context!.Logger.LogInformation("Deleting report: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        await reportService.DeleteReportAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(new { success = true }, JsonOptions);
    }

    private async Task<JsonElement> ExecuteDuplicateReportAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DuplicateReportRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid duplicateReport request payload", nameof(payload));

        _context!.Logger.LogInformation("Duplicating report: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.DuplicateReportAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteListReportsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ListReportsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid listReports request payload", nameof(payload));

        _context!.Logger.LogInformation("Listing reports");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.ListReportsAsync(request.ConnectionId, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteExecuteReportAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ExecuteReportRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid executeReport request payload", nameof(payload));

        _context!.Logger.LogInformation("Executing report: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.ExecuteReportAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteReorderReportsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ReorderReportsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid reorderReports request payload", nameof(payload));

        _context!.Logger.LogInformation("Reordering reports");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        await reportService.ReorderReportsAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(new { success = true }, JsonOptions);
    }

    private async Task<JsonElement> ExecuteCreateReportGroupAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<CreateReportGroupRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid createReportGroup request payload", nameof(payload));

        _context!.Logger.LogInformation("Creating report group: {Name}", request.Name);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.CreateReportGroupAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteUpdateReportGroupAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<UpdateReportGroupRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid updateReportGroup request payload", nameof(payload));

        _context!.Logger.LogInformation("Updating report group: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.UpdateReportGroupAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteDeleteReportGroupAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<DeleteReportGroupRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid deleteReportGroup request payload", nameof(payload));

        _context!.Logger.LogInformation("Deleting report group: {Id}", request.Id);

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        await reportService.DeleteReportGroupAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(new { success = true }, JsonOptions);
    }

    private async Task<JsonElement> ExecuteReorderReportGroupsAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ReorderReportGroupsRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid reorderReportGroups request payload", nameof(payload));

        _context!.Logger.LogInformation("Reordering report groups");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        await reportService.ReorderReportGroupsAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(new { success = true }, JsonOptions);
    }

    private async Task<JsonElement> ExecuteExportConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ExportConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid exportConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Exporting configuration");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.ExportConfigAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteImportConfigAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ImportConfigRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid importConfig request payload", nameof(payload));

        _context!.Logger.LogInformation("Importing configuration");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.ImportConfigAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
    }

    private async Task<JsonElement> ExecuteGenerateReportOutputAsync(string payload, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<GenerateReportOutputRequest>(payload, JsonOptions)
            ?? throw new ArgumentException("Invalid generateReportOutput request payload", nameof(payload));

        _context!.Logger.LogInformation("Generating report output");

        await using var dbContext = CreateDbContext(request.ConnectionId);
        var queryService = new QueryService(dbContext);
        var reportService = new ReportService(dbContext, _context.Logger, queryService);
        var response = await reportService.GenerateReportOutputAsync(request, cancellationToken);

        return JsonSerializer.SerializeToElement(response, JsonOptions);
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
