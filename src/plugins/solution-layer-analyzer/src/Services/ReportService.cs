using System.Text.Json;
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
/// Service for managing reports and report groups
/// </summary>
public class ReportService
{
    private readonly AnalyzerDbContext _context;
    private readonly ILogger _logger;
    private readonly QueryService _queryService;

    public ReportService(AnalyzerDbContext context, ILogger logger, QueryService queryService)
    {
        _context = context;
        _logger = logger;
        _queryService = queryService;
    }

    /// <summary>
    /// Save a new report
    /// </summary>
    public async Task<ReportDto> SaveReportAsync(SaveReportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving new report: {Name}", request.Name);

        // Get the next display order
        var maxOrder = await _context.Reports
            .Where(r => r.ConnectionId == request.ConnectionId && r.GroupId == request.GroupId)
            .MaxAsync(r => (int?)r.DisplayOrder, cancellationToken) ?? 0;

        var report = new Report
        {
            ConnectionId = request.ConnectionId,
            Name = request.Name,
            Description = request.Description,
            GroupId = request.GroupId,
            Severity = request.Severity,
            RecommendedAction = request.RecommendedAction,
            QueryJson = request.QueryJson,
            OriginatingIndexHash = request.OriginatingIndexHash,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetReportDtoAsync(report.Id, request.ConnectionId, cancellationToken);
    }

    /// <summary>
    /// Update an existing report
    /// </summary>
    public async Task<ReportDto> UpdateReportAsync(UpdateReportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating report: {Id}", request.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report {request.Id} not found");

        if (request.Name != null) report.Name = request.Name;
        if (request.Description != null) report.Description = request.Description;
        if (request.GroupId.HasValue) report.GroupId = request.GroupId;
        if (request.Severity.HasValue) report.Severity = request.Severity.Value;
        if (request.RecommendedAction != null) report.RecommendedAction = request.RecommendedAction;
        if (request.QueryJson != null) report.QueryJson = request.QueryJson;
        
        report.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetReportDtoAsync(report.Id, request.ConnectionId, cancellationToken);
    }

    /// <summary>
    /// Delete a report
    /// </summary>
    public async Task DeleteReportAsync(DeleteReportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting report: {Id}", request.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report {request.Id} not found");

        _context.Reports.Remove(report);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Duplicate a report
    /// </summary>
    public async Task<ReportDto> DuplicateReportAsync(DuplicateReportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Duplicating report: {Id}", request.Id);

        var original = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report {request.Id} not found");

        // Get the next display order
        var maxOrder = await _context.Reports
            .Where(r => r.ConnectionId == request.ConnectionId && r.GroupId == original.GroupId)
            .MaxAsync(r => (int?)r.DisplayOrder, cancellationToken) ?? 0;

        var duplicate = new Report
        {
            ConnectionId = original.ConnectionId,
            Name = request.NewName ?? $"{original.Name} (Copy)",
            Description = original.Description,
            GroupId = original.GroupId,
            Severity = original.Severity,
            RecommendedAction = original.RecommendedAction,
            QueryJson = original.QueryJson,
            OriginatingIndexHash = original.OriginatingIndexHash,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reports.Add(duplicate);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetReportDtoAsync(duplicate.Id, request.ConnectionId, cancellationToken);
    }

    /// <summary>
    /// Reorder reports
    /// </summary>
    public async Task ReorderReportsAsync(ReorderReportsRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reordering {Count} reports", request.Reports.Count);

        var reportIds = request.Reports.Select(r => r.Id).ToList();
        var reports = await _context.Reports
            .Where(r => reportIds.Contains(r.Id) && r.ConnectionId == request.ConnectionId)
            .ToListAsync(cancellationToken);

        foreach (var orderUpdate in request.Reports)
        {
            var report = reports.FirstOrDefault(r => r.Id == orderUpdate.Id);
            if (report != null)
            {
                report.DisplayOrder = orderUpdate.DisplayOrder;
                if (orderUpdate.GroupId != report.GroupId)
                {
                    report.GroupId = orderUpdate.GroupId;
                }
                report.ModifiedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Execute a saved report
    /// </summary>
    public async Task<ExecuteReportResponse> ExecuteReportAsync(ExecuteReportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing report: {Id}", request.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report {request.Id} not found");

        // Deserialize the filter query
        var filterNode = JsonSerializer.Deserialize<FilterNode>(report.QueryJson);
        if (filterNode == null)
        {
            throw new InvalidOperationException("Invalid query JSON in report");
        }

        // Execute the query using QueryService
        var queryRequest = new QueryRequest
        {
            Filters = filterNode
        };

        var queryResponse = await _queryService.QueryAsync(queryRequest, cancellationToken);

        // Map to component results
        var componentResults = queryResponse.Rows.Select(c =>
        {
            // Map component type string to code
            var typeCode = c.ComponentType switch
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

            return new ReportComponentResult
            {
                ComponentId = c.ComponentId.ToString(),
                ComponentType = typeCode,
                ComponentTypeName = c.ComponentType,
                LogicalName = c.LogicalName,
                DisplayName = c.DisplayName,
                Solutions = c.LayerSequence
            };
        }).ToList();

        // Update last executed timestamp
        report.LastExecutedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new ExecuteReportResponse
        {
            ReportId = report.Id,
            ReportName = report.Name,
            Severity = report.Severity,
            TotalMatches = componentResults.Count,
            Components = componentResults,
            ExecutedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// List all reports organized by groups
    /// </summary>
    public async Task<ListReportsResponse> ListReportsAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing reports for connection: {ConnectionId}", connectionId);

        var groups = await _context.ReportGroups
            .Where(g => g.ConnectionId == connectionId)
            .Include(g => g.Reports)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync(cancellationToken);

        var ungroupedReports = await _context.Reports
            .Where(r => r.ConnectionId == connectionId && r.GroupId == null)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        var response = new ListReportsResponse
        {
            Groups = groups.Select(g => new ReportGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                DisplayOrder = g.DisplayOrder,
                CreatedAt = g.CreatedAt,
                ModifiedAt = g.ModifiedAt,
                Reports = g.Reports.OrderBy(r => r.DisplayOrder).Select(MapToDto).ToList()
            }).ToList(),
            UngroupedReports = ungroupedReports.Select(MapToDto).ToList()
        };

        return response;
    }

    /// <summary>
    /// Create a new report group
    /// </summary>
    public async Task<ReportGroupDto> CreateReportGroupAsync(CreateReportGroupRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating report group: {Name}", request.Name);

        var maxOrder = await _context.ReportGroups
            .Where(g => g.ConnectionId == request.ConnectionId)
            .MaxAsync(g => (int?)g.DisplayOrder, cancellationToken) ?? 0;

        var group = new ReportGroup
        {
            ConnectionId = request.ConnectionId,
            Name = request.Name,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReportGroups.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        return new ReportGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            DisplayOrder = group.DisplayOrder,
            CreatedAt = group.CreatedAt,
            Reports = new List<ReportDto>()
        };
    }

    /// <summary>
    /// Update a report group
    /// </summary>
    public async Task<ReportGroupDto> UpdateReportGroupAsync(UpdateReportGroupRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating report group: {Id}", request.Id);

        var group = await _context.ReportGroups
            .Include(g => g.Reports)
            .FirstOrDefaultAsync(g => g.Id == request.Id && g.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report group {request.Id} not found");

        if (request.Name != null) group.Name = request.Name;
        group.ModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new ReportGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            DisplayOrder = group.DisplayOrder,
            CreatedAt = group.CreatedAt,
            ModifiedAt = group.ModifiedAt,
            Reports = group.Reports.OrderBy(r => r.DisplayOrder).Select(MapToDto).ToList()
        };
    }

    /// <summary>
    /// Delete a report group (reports in the group will become ungrouped)
    /// </summary>
    public async Task DeleteReportGroupAsync(DeleteReportGroupRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting report group: {Id}", request.Id);

        var group = await _context.ReportGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id && g.ConnectionId == request.ConnectionId, cancellationToken)
            ?? throw new ArgumentException($"Report group {request.Id} not found");

        _context.ReportGroups.Remove(group);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reorder report groups
    /// </summary>
    public async Task ReorderReportGroupsAsync(ReorderReportGroupsRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reordering {Count} report groups", request.Groups.Count);

        var groupIds = request.Groups.Select(g => g.Id).ToList();
        var groups = await _context.ReportGroups
            .Where(g => groupIds.Contains(g.Id) && g.ConnectionId == request.ConnectionId)
            .ToListAsync(cancellationToken);

        foreach (var orderUpdate in request.Groups)
        {
            var group = groups.FirstOrDefault(g => g.Id == orderUpdate.Id);
            if (group != null)
            {
                group.DisplayOrder = orderUpdate.DisplayOrder;
                group.ModifiedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Export analyzer configuration to YAML
    /// </summary>
    public async Task<ExportConfigResponse> ExportConfigAsync(ExportConfigRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting configuration for connection: {ConnectionId}", request.ConnectionId);

        // Get the saved index config to populate source/target solutions
        var indexConfig = await _context.SavedIndexConfigs
            .Where(c => c.ConnectionId == request.ConnectionId)
            .OrderByDescending(c => c.LastUsedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var groups = await _context.ReportGroups
            .Where(g => g.ConnectionId == request.ConnectionId)
            .Include(g => g.Reports)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync(cancellationToken);

        var ungroupedReports = await _context.Reports
            .Where(r => r.ConnectionId == request.ConnectionId && r.GroupId == null)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken);

        var config = new AnalyzerConfig
        {
            SourceSolutions = indexConfig?.SourceSolutions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            TargetSolutions = indexConfig?.TargetSolutions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            ComponentTypes = indexConfig?.ComponentTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).ToList(),
            ReportGroups = groups.Select(g => new ConfigReportGroup
            {
                Name = g.Name,
                DisplayOrder = g.DisplayOrder,
                Reports = g.Reports.OrderBy(r => r.DisplayOrder).Select(MapToConfigReport).ToList()
            }).ToList(),
            UngroupedReports = ungroupedReports.Select(MapToConfigReport).ToList()
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(config);

        if (!string.IsNullOrEmpty(request.FilePath))
        {
            await File.WriteAllTextAsync(request.FilePath, yaml, cancellationToken);
        }

        return new ExportConfigResponse
        {
            ConfigYaml = yaml,
            FilePath = request.FilePath
        };
    }

    /// <summary>
    /// Import analyzer configuration from YAML
    /// </summary>
    public async Task<ImportConfigResponse> ImportConfigAsync(ImportConfigRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing configuration for connection: {ConnectionId}", request.ConnectionId);

        var yaml = request.ConfigYaml;
        if (string.IsNullOrEmpty(yaml) && !string.IsNullOrEmpty(request.FilePath))
        {
            yaml = await File.ReadAllTextAsync(request.FilePath, cancellationToken);
        }

        if (string.IsNullOrEmpty(yaml))
        {
            throw new ArgumentException("Either ConfigYaml or FilePath must be provided");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var config = deserializer.Deserialize<AnalyzerConfig>(yaml);

        var warnings = new List<string>();
        var groupsImported = 0;
        var reportsImported = 0;

        // Import report groups
        var groupIdMap = new Dictionary<string, int>(); // Old name to new ID
        foreach (var configGroup in config.ReportGroups)
        {
            var group = new ReportGroup
            {
                ConnectionId = request.ConnectionId,
                Name = configGroup.Name,
                DisplayOrder = configGroup.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReportGroups.Add(group);
            await _context.SaveChangesAsync(cancellationToken);
            
            groupIdMap[configGroup.Name] = group.Id;
            groupsImported++;

            // Import reports in this group
            foreach (var configReport in configGroup.Reports)
            {
                var report = new Report
                {
                    ConnectionId = request.ConnectionId,
                    Name = configReport.Name,
                    Description = configReport.Description,
                    GroupId = group.Id,
                    Severity = configReport.Severity,
                    RecommendedAction = configReport.RecommendedAction,
                    QueryJson = configReport.QueryJson,
                    DisplayOrder = configReport.DisplayOrder,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reports.Add(report);
                reportsImported++;
            }
        }

        // Import ungrouped reports
        foreach (var configReport in config.UngroupedReports)
        {
            var report = new Report
            {
                ConnectionId = request.ConnectionId,
                Name = configReport.Name,
                Description = configReport.Description,
                GroupId = null,
                Severity = configReport.Severity,
                RecommendedAction = configReport.RecommendedAction,
                QueryJson = configReport.QueryJson,
                DisplayOrder = configReport.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            reportsImported++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ImportConfigResponse
        {
            GroupsImported = groupsImported,
            ReportsImported = reportsImported,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Generate report output in YAML or JSON format
    /// </summary>
    public async Task<GenerateReportOutputResponse> GenerateReportOutputAsync(GenerateReportOutputRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating report output for connection: {ConnectionId}", request.ConnectionId);

        var reportIds = request.ReportId.HasValue 
            ? new List<int> { request.ReportId.Value }
            : request.ReportIds ?? new List<int>();

        if (!reportIds.Any())
        {
            // Get all reports if none specified
            reportIds = await _context.Reports
                .Where(r => r.ConnectionId == request.ConnectionId)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
        }

        var reportResults = new List<ReportExecutionResult>();
        var totalCritical = 0;
        var totalWarning = 0;
        var totalInfo = 0;
        var allComponentIds = new HashSet<string>();

        foreach (var reportId in reportIds)
        {
            var report = await _context.Reports
                .Include(r => r.Group)
                .FirstOrDefaultAsync(r => r.Id == reportId && r.ConnectionId == request.ConnectionId, cancellationToken);

            if (report == null) continue;

            // Execute the report
            var executeRequest = new ExecuteReportRequest
            {
                Id = reportId,
                ConnectionId = request.ConnectionId
            };
            var executeResponse = await ExecuteReportAsync(executeRequest, cancellationToken);

            // Map to detailed results based on verbosity
            var detailedComponents = await MapToDetailedComponentsAsync(
                executeResponse.Components, 
                request.Verbosity, 
                request.ConnectionId,
                cancellationToken);

            var result = new ReportExecutionResult
            {
                Name = report.Name,
                Group = report.Group?.Name,
                Severity = report.Severity,
                RecommendedAction = report.RecommendedAction,
                TotalMatches = executeResponse.TotalMatches,
                Components = detailedComponents
            };

            reportResults.Add(result);

            // Update summary counts
            foreach (var comp in executeResponse.Components)
            {
                allComponentIds.Add(comp.ComponentId);
            }

            if (report.Severity == ReportSeverity.Critical)
                totalCritical += executeResponse.TotalMatches;
            else if (report.Severity == ReportSeverity.Warning)
                totalWarning += executeResponse.TotalMatches;
            else
                totalInfo += executeResponse.TotalMatches;
        }

        var reportOutput = new ReportOutput
        {
            GeneratedAt = DateTime.UtcNow,
            ConnectionId = request.ConnectionId,
            Verbosity = request.Verbosity,
            Reports = reportResults,
            Summary = new ReportSummary
            {
                TotalReports = reportResults.Count,
                CriticalFindings = totalCritical,
                WarningFindings = totalWarning,
                InformationalFindings = totalInfo,
                TotalComponents = allComponentIds.Count
            }
        };

        string outputContent;
        if (request.Format == ReportOutputFormat.Yaml)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            outputContent = serializer.Serialize(reportOutput);
        }
        else if (request.Format == ReportOutputFormat.Json)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            outputContent = JsonSerializer.Serialize(reportOutput, options);
        }
        else // CSV
        {
            outputContent = CsvHelper.SerializeReportOutput(reportOutput, request.Verbosity);
        }

        if (!string.IsNullOrEmpty(request.FilePath))
        {
            await File.WriteAllTextAsync(request.FilePath, outputContent, cancellationToken);
        }

        return new GenerateReportOutputResponse
        {
            OutputContent = outputContent,
            FilePath = request.FilePath,
            Format = request.Format
        };
    }

    private async Task<List<DetailedComponentResult>> MapToDetailedComponentsAsync(
        List<ReportComponentResult> components, 
        ReportVerbosity verbosity,
        string connectionId,
        CancellationToken cancellationToken)
    {
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
                MakePortalUrl = $"https://make.powerapps.com/environments/{connectionId}"
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

    private async Task<ReportDto> GetReportDtoAsync(int reportId, string connectionId, CancellationToken cancellationToken)
    {
        var report = await _context.Reports
            .Include(r => r.Group)
            .FirstOrDefaultAsync(r => r.Id == reportId && r.ConnectionId == connectionId, cancellationToken)
            ?? throw new ArgumentException($"Report {reportId} not found");

        return MapToDto(report);
    }

    private static ReportDto MapToDto(Report report)
    {
        return new ReportDto
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            GroupId = report.GroupId,
            GroupName = report.Group?.Name,
            Severity = report.Severity,
            RecommendedAction = report.RecommendedAction,
            QueryJson = report.QueryJson,
            DisplayOrder = report.DisplayOrder,
            OriginatingIndexHash = report.OriginatingIndexHash,
            CreatedAt = report.CreatedAt,
            ModifiedAt = report.ModifiedAt,
            LastExecutedAt = report.LastExecutedAt
        };
    }

    private static ConfigReport MapToConfigReport(Report report)
    {
        return new ConfigReport
        {
            Name = report.Name,
            Description = report.Description,
            Severity = report.Severity,
            RecommendedAction = report.RecommendedAction,
            QueryJson = report.QueryJson,
            DisplayOrder = report.DisplayOrder
        };
    }
}
