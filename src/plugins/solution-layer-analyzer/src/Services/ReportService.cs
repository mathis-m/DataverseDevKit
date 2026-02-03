using System.Text.Json;
using System.Xml.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ddk.SolutionLayerAnalyzer.Data;
using Ddk.SolutionLayerAnalyzer.Models;
using Ddk.SolutionLayerAnalyzer.DTOs;
using Ddk.SolutionLayerAnalyzer.Filters;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Service for managing reports - stateless operations only.
/// No database persistence - report configurations are managed in the UI.
/// </summary>
public class ReportService
{
    private readonly AnalyzerDbContext? _context;
    private readonly ILogger _logger;
    private readonly QueryService? _queryService;

    public ReportService(AnalyzerDbContext? context, ILogger logger, QueryService? queryService)
    {
        _context = context;
        _logger = logger;
        _queryService = queryService;
    }

    // ============================================================================
    // Stateless Report Operations (UI-centric, no database)
    // ============================================================================

    /// <summary>
    /// Parse a report configuration from file content.
    /// Supports JSON, YAML, and XML formats.
    /// </summary>
    public ParseReportConfigResponse ParseConfig(ParseReportConfigRequest request)
    {
        var response = new ParseReportConfigResponse();
        
        try
        {
            var format = request.Format ?? DetectFormat(request.Content);
            
            response.Config = format switch
            {
                ReportConfigFormat.Json => ParseJsonConfig(request.Content),
                ReportConfigFormat.Yaml => ParseYamlConfig(request.Content),
                ReportConfigFormat.Xml => ParseXmlConfig(request.Content),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse report config");
            response.Errors.Add($"Parse error: {ex.Message}");
        }
        
        return response;
    }

    /// <summary>
    /// Serialize a report configuration to the specified format.
    /// </summary>
    public SerializeReportConfigResponse SerializeConfig(SerializeReportConfigRequest request)
    {
        var content = request.Format switch
        {
            ReportConfigFormat.Json => SerializeJsonConfig(request.Config),
            ReportConfigFormat.Yaml => SerializeYamlConfig(request.Config),
            ReportConfigFormat.Xml => SerializeXmlConfig(request.Config),
            _ => throw new ArgumentException($"Unsupported format: {request.Format}")
        };
        
        return new SerializeReportConfigResponse { Content = content };
    }

    /// <summary>
    /// Execute reports from an in-memory configuration.
    /// This is the stateless version that takes the full config from the request.
    /// </summary>
    public async Task<ReportCompletionEvent> ExecuteReportsFromConfigAsync(
        ExecuteReportsRequest request,
        IProgress<ReportProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing reports from config, operation: {OperationId}", request.OperationId);

        var reportResults = new List<ReportExecutionResult>();
        var totalCritical = 0;
        var totalWarning = 0;
        var totalInfo = 0;
        var allComponentIds = new HashSet<string>();

        // Collect all reports
        var allReports = new List<(ReportDefinition Report, string? GroupName)>();
        
        foreach (var group in request.Config.ReportGroups)
        {
            foreach (var report in group.Reports)
            {
                allReports.Add((report, group.Name));
            }
        }
        
        foreach (var report in request.Config.UngroupedReports)
        {
            allReports.Add((report, null));
        }

        var totalReports = allReports.Count;
        var currentReport = 0;

        // Report starting phase
        progress?.Report(new ReportProgressEvent
        {
            OperationId = request.OperationId ?? string.Empty,
            CurrentReport = 0,
            TotalReports = totalReports,
            Phase = "starting",
            Percent = 0
        });

        foreach (var (report, groupName) in allReports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentReport++;

            // Report executing phase
            progress?.Report(new ReportProgressEvent
            {
                OperationId = request.OperationId ?? string.Empty,
                CurrentReport = currentReport,
                TotalReports = totalReports,
                CurrentReportName = report.Name,
                Phase = "executing",
                Percent = (int)((currentReport - 1) * 100.0 / totalReports)
            });

            try
            {
                // Parse the filter from queryJson
                FilterNode? filterNode = null;
                if (!string.IsNullOrEmpty(report.QueryJson) && report.QueryJson != "null")
                {
                    filterNode = JsonSerializer.Deserialize<FilterNode>(report.QueryJson);
                }

                // Execute the query
                var queryRequest = new QueryRequest { Filters = filterNode };
                
                if (_queryService == null)
                {
                    throw new InvalidOperationException("Query service is required for report execution");
                }
                
                var queryResponse = await _queryService.QueryAsync(queryRequest, cancellationToken);

                // Map to component results
                var componentResults = queryResponse.Rows.Select(c => new ReportComponentResult
                {
                    ComponentId = c.ComponentId.ToString(),
                    ComponentType = MapComponentTypeToCode(c.ComponentType),
                    ComponentTypeName = c.ComponentType,
                    LogicalName = c.LogicalName,
                    DisplayName = c.DisplayName,
                    Solutions = c.LayerSequence
                }).ToList();

                // Map to detailed results based on verbosity
                var detailedComponents = await MapToDetailedComponentsAsync(
                    componentResults,
                    request.Verbosity,
                    request.ConnectionId,
                    cancellationToken);

                var result = new ReportExecutionResult
                {
                    Name = report.Name,
                    Group = groupName,
                    Severity = report.Severity,
                    RecommendedAction = report.RecommendedAction,
                    TotalMatches = componentResults.Count,
                    Components = detailedComponents
                };

                reportResults.Add(result);

                // Update summary counts
                foreach (var comp in componentResults)
                {
                    allComponentIds.Add(comp.ComponentId);
                }

                if (report.Severity == ReportSeverity.Critical)
                    totalCritical += componentResults.Count;
                else if (report.Severity == ReportSeverity.Warning)
                    totalWarning += componentResults.Count;
                else
                    totalInfo += componentResults.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute report: {ReportName}", report.Name);
                // Continue with other reports
            }
        }

        var summary = new ReportSummary
        {
            TotalReports = reportResults.Count,
            CriticalFindings = totalCritical,
            WarningFindings = totalWarning,
            InformationalFindings = totalInfo,
            TotalComponents = allComponentIds.Count
        };

        // Generate output content if requested
        string? outputContent = null;
        if (request.GenerateFile && request.Format.HasValue)
        {
            progress?.Report(new ReportProgressEvent
            {
                OperationId = request.OperationId ?? string.Empty,
                CurrentReport = totalReports,
                TotalReports = totalReports,
                Phase = "generating-output",
                Percent = 95
            });

            var reportOutput = new ReportOutput
            {
                GeneratedAt = DateTime.UtcNow,
                ConnectionId = request.ConnectionId,
                Verbosity = request.Verbosity,
                Reports = reportResults,
                Summary = summary
            };

            outputContent = request.Format.Value switch
            {
                ReportOutputFormat.Yaml => SerializeYamlOutput(reportOutput),
                ReportOutputFormat.Json => SerializeJsonOutput(reportOutput),
                ReportOutputFormat.Csv => CsvHelper.SerializeReportOutput(reportOutput, request.Verbosity),
                _ => SerializeJsonOutput(reportOutput)
            };
        }

        return new ReportCompletionEvent
        {
            OperationId = request.OperationId ?? string.Empty,
            Success = true,
            Summary = summary,
            Reports = reportResults,
            OutputContent = outputContent,
            OutputFormat = request.Format
        };
    }

    // ============================================================================
    // Private Helpers for Stateless Operations
    // ============================================================================

    private static ReportConfigFormat DetectFormat(string content)
    {
        content = content.TrimStart();
        
        if (content.StartsWith('{') || content.StartsWith('['))
            return ReportConfigFormat.Json;
        
        if (content.StartsWith("<?xml") || content.StartsWith("<"))
            return ReportConfigFormat.Xml;
        
        // Default to YAML
        return ReportConfigFormat.Yaml;
    }

    private ReportConfigDefinition ParseJsonConfig(string content)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        return JsonSerializer.Deserialize<ReportConfigDefinition>(content, options) 
            ?? new ReportConfigDefinition();
    }

    private ReportConfigDefinition ParseYamlConfig(string content)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<ReportConfigDefinition>(content) 
            ?? new ReportConfigDefinition();
    }

    private ReportConfigDefinition ParseXmlConfig(string content)
    {
        var serializer = new XmlSerializer(typeof(ReportConfigDefinition));
        using var reader = new StringReader(content);
        return (ReportConfigDefinition?)serializer.Deserialize(reader) 
            ?? new ReportConfigDefinition();
    }

    private string SerializeJsonConfig(ReportConfigDefinition config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(config, options);
    }

    private string SerializeYamlConfig(ReportConfigDefinition config)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        return serializer.Serialize(config);
    }

    private string SerializeXmlConfig(ReportConfigDefinition config)
    {
        var serializer = new XmlSerializer(typeof(ReportConfigDefinition));
        using var writer = new StringWriter();
        serializer.Serialize(writer, config);
        return writer.ToString();
    }

    private string SerializeJsonOutput(ReportOutput output)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(output, options);
    }

    private string SerializeYamlOutput(ReportOutput output)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        return serializer.Serialize(output);
    }

    private static int MapComponentTypeToCode(string componentType)
    {
        return componentType switch
        {
            "Entity" => ComponentTypeCodes.Entity,
            "Attribute" => ComponentTypeCodes.Attribute,
            "SystemForm" => ComponentTypeCodes.SystemForm,
            "SavedQuery" => ComponentTypeCodes.SavedQuery,
            "SavedQueryVisualization" => ComponentTypeCodes.SavedQueryVisualization,
            "RibbonCustomization" => ComponentTypeCodes.RibbonCustomization,
            "WebResource" => ComponentTypeCodes.WebResource,
            "SDKMessageProcessingStep" => ComponentTypeCodes.SDKMessageProcessingStep,
            "Workflow" => ComponentTypeCodes.Workflow,
            "AppModule" => ComponentTypeCodes.AppModule,
            "SiteMap" => ComponentTypeCodes.SiteMap,
            "OptionSet" => ComponentTypeCodes.OptionSet,
            _ => 0
        };
    }

    // ============================================================================
    // Private Helpers (Legacy DB operations)
    // ============================================================================

    private async Task<List<DetailedComponentResult>> MapToDetailedComponentsAsync(
        List<ReportComponentResult> components, 
        ReportVerbosity verbosity,
        string connectionId,
        CancellationToken cancellationToken)
    {
        if (_context == null)
        {
            throw new InvalidOperationException("Database context is required for detailed component mapping");
        }

        var results = new List<DetailedComponentResult>();

        foreach (var component in components)
        {
            var detailed = new DetailedComponentResult
            {
                ComponentId = component.ComponentId,
                ComponentType = component.ComponentType,
                ComponentTypeName = component.ComponentTypeName,
                LogicalName = component.LogicalName,
                DisplayName = component.DisplayName,
                Solutions = component.Solutions,
                MakePortalUrl = GenerateMakePortalUrl(connectionId, component)
            };

            // Add layer information based on verbosity
            if (verbosity != ReportVerbosity.Basic)
            {
                var dbComponent = await _context.Components
                    .Include(c => c.Layers)
                    .ThenInclude(l => l.Attributes)
                    .FirstOrDefaultAsync(c => c.ComponentId.ToString() == component.ComponentId, cancellationToken);

                if (dbComponent != null)
                {
                    detailed.Layers = dbComponent.Layers
                        .OrderBy(l => l.Ordinal)
                        .Select(l => new LayerInfo
                        {
                            SolutionName = l.SolutionName,
                            Ordinal = l.Ordinal,
                            ChangedAttributes = verbosity == ReportVerbosity.Verbose
                                ? l.Attributes
                                    .Where(a => a.IsChanged)
                                    .Select(a => new AttributeChange
                                    {
                                        AttributeName = a.AttributeName,
                                        OldValue = a.RawValue,
                                        NewValue = a.AttributeValue
                                    }).ToList()
                                : (verbosity == ReportVerbosity.Medium
                                    ? l.Attributes
                                        .Where(a => a.IsChanged)
                                        .Select(a => new AttributeChange
                                        {
                                            AttributeName = a.AttributeName
                                        }).ToList()
                                    : null)
                        }).ToList();
                }
            }

            results.Add(detailed);
        }

        return results;
    }

    private static string GenerateMakePortalUrl(string connectionId, ReportComponentResult component)
    {
        // Always link to the layers view for all component types
        // Format: https://make.powerapps.com/environments/<env id>/solutions/fd140aaf-4df4-11dd-bd17-0019b9312238/objects/all/<component id>/layers
        return $"https://make.powerapps.com/environments/{connectionId}/solutions/fd140aaf-4df4-11dd-bd17-0019b9312238/objects/all/{component.ComponentId}/layers";
    }
}
