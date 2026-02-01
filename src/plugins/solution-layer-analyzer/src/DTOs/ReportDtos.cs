using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

// Request DTOs for report operations

/// <summary>
/// Request to save a query/filter as a report
/// </summary>
public class SaveReportRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? GroupId { get; set; }
    public ReportSeverity Severity { get; set; } = ReportSeverity.Information;
    public string? RecommendedAction { get; set; }
    public string QueryJson { get; set; } = string.Empty;
    public string? OriginatingIndexHash { get; set; }
}

/// <summary>
/// Request to update an existing report
/// </summary>
public class UpdateReportRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? GroupId { get; set; }
    public ReportSeverity? Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public string? QueryJson { get; set; }
}

/// <summary>
/// Request to delete a report
/// </summary>
public class DeleteReportRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to duplicate a report
/// </summary>
public class DuplicateReportRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? NewName { get; set; }
}

/// <summary>
/// Request to reorder reports
/// </summary>
public class ReorderReportsRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public List<ReportOrder> Reports { get; set; } = new();
}

public class ReportOrder
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public int? GroupId { get; set; }
}

/// <summary>
/// Request to execute a saved report
/// </summary>
public class ExecuteReportRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to list all reports
/// </summary>
public class ListReportsRequest
{
    public string ConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a report group
/// </summary>
public class CreateReportGroupRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a report group
/// </summary>
public class UpdateReportGroupRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? Name { get; set; }
}

/// <summary>
/// Request to delete a report group
/// </summary>
public class DeleteReportGroupRequest
{
    public int Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
}

/// <summary>
/// Request to reorder report groups
/// </summary>
public class ReorderReportGroupsRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public List<GroupOrder> Groups { get; set; } = new();
}

public class GroupOrder
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request to export analyzer configuration to YAML
/// </summary>
public class ExportConfigRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

/// <summary>
/// Request to import analyzer configuration from YAML
/// </summary>
public class ImportConfigRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? ConfigYaml { get; set; }
}

// Response DTOs

/// <summary>
/// Response with report data
/// </summary>
public class ReportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public ReportSeverity Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public string QueryJson { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? OriginatingIndexHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastExecutedAt { get; set; }
}

/// <summary>
/// Response with report group data
/// </summary>
public class ReportGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<ReportDto> Reports { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Response with all reports organized by groups
/// </summary>
public class ListReportsResponse
{
    public List<ReportGroupDto> Groups { get; set; } = new();
    public List<ReportDto> UngroupedReports { get; set; } = new();
}

/// <summary>
/// Response from executing a report
/// </summary>
public class ExecuteReportResponse
{
    public int ReportId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public ReportSeverity Severity { get; set; }
    public int TotalMatches { get; set; }
    public List<ReportComponentResult> Components { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
}

public class ReportComponentResult
{
    public string ComponentId { get; set; } = string.Empty;
    public int ComponentType { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;
    public string? LogicalName { get; set; }
    public string? DisplayName { get; set; }
    public List<string> Solutions { get; set; } = new();
}

/// <summary>
/// Response from exporting configuration
/// </summary>
public class ExportConfigResponse
{
    public string ConfigYaml { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

/// <summary>
/// Response from importing configuration
/// </summary>
public class ImportConfigResponse
{
    public int GroupsImported { get; set; }
    public int ReportsImported { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Request to generate a report output file
/// </summary>
public class GenerateReportOutputRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public int? ReportId { get; set; }
    public List<int>? ReportIds { get; set; }
    public ReportOutputFormat Format { get; set; } = ReportOutputFormat.Yaml;
    public ReportVerbosity Verbosity { get; set; } = ReportVerbosity.Basic;
    public string? FilePath { get; set; }
}

/// <summary>
/// Report output format
/// </summary>
public enum ReportOutputFormat
{
    Yaml,
    Json,
    Csv
}

/// <summary>
/// Report verbosity level
/// </summary>
public enum ReportVerbosity
{
    /// <summary>
    /// Basic - Components only with summary
    /// </summary>
    Basic,
    
    /// <summary>
    /// Medium - Components + changed layer attribute list
    /// </summary>
    Medium,
    
    /// <summary>
    /// Verbose - Components + layer attributes with full changed values
    /// </summary>
    Verbose
}

/// <summary>
/// Response from generating report output
/// </summary>
public class GenerateReportOutputResponse
{
    public string OutputContent { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public ReportOutputFormat Format { get; set; }
}

/// <summary>
/// Detailed report output structure
/// </summary>
public class ReportOutput
{
    public DateTime GeneratedAt { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public ReportVerbosity Verbosity { get; set; }
    public List<ReportExecutionResult> Reports { get; set; } = new();
    public ReportSummary Summary { get; set; } = new();
}

/// <summary>
/// Individual report execution result in output
/// </summary>
public class ReportExecutionResult
{
    public string Name { get; set; } = string.Empty;
    public string? Group { get; set; }
    public ReportSeverity Severity { get; set; }
    public string? RecommendedAction { get; set; }
    public int TotalMatches { get; set; }
    public List<DetailedComponentResult> Components { get; set; } = new();
}

/// <summary>
/// Detailed component result with layer information
/// </summary>
public class DetailedComponentResult
{
    public string ComponentId { get; set; } = string.Empty;
    public int ComponentType { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;
    public string? LogicalName { get; set; }
    public string? DisplayName { get; set; }
    public List<string>? Solutions { get; set; }
    public List<LayerInfo>? Layers { get; set; }
    public string? MakePortalUrl { get; set; }
}

/// <summary>
/// Layer information for verbose output
/// </summary>
public class LayerInfo
{
    public string SolutionName { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public List<AttributeChange>? ChangedAttributes { get; set; }
}

/// <summary>
/// Attribute change information for verbose output
/// </summary>
public class AttributeChange
{
    public string AttributeName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// Summary of all report executions
/// </summary>
public class ReportSummary
{
    public int TotalReports { get; set; }
    public int CriticalFindings { get; set; }
    public int WarningFindings { get; set; }
    public int InformationalFindings { get; set; }
    public int TotalComponents { get; set; }
}
