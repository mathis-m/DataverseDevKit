using Ddk.SolutionLayerAnalyzer.Models;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

// ============================================================================
// Stateless Report Configuration DTOs (UI-centric, no database)
// ============================================================================

/// <summary>
/// A single report definition (in-memory, from UI)
/// </summary>
public class ReportDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportSeverity Severity { get; set; } = ReportSeverity.Information;
    public string? RecommendedAction { get; set; }
    public string QueryJson { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

/// <summary>
/// A group of reports (in-memory, from UI)
/// </summary>
public class ReportGroupDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<ReportDefinition> Reports { get; set; } = new();
}

/// <summary>
/// Full report configuration (in-memory, from UI)
/// </summary>
public class ReportConfigDefinition
{
    public List<string> SourceSolutions { get; set; } = new();
    public List<string> TargetSolutions { get; set; } = new();
    public List<int>? ComponentTypes { get; set; }
    public List<ReportGroupDefinition> ReportGroups { get; set; } = new();
    public List<ReportDefinition> UngroupedReports { get; set; } = new();
}

/// <summary>
/// Supported formats for report config serialization
/// </summary>
public enum ReportConfigFormat
{
    Json,
    Yaml,
    Xml
}

/// <summary>
/// Request to parse a report configuration from file content
/// </summary>
public class ParseReportConfigRequest
{
    public string Content { get; set; } = string.Empty;
    public ReportConfigFormat? Format { get; set; } // Auto-detect if null
}

/// <summary>
/// Response from parsing a report configuration
/// </summary>
public class ParseReportConfigResponse
{
    public ReportConfigDefinition Config { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Request to serialize a report configuration
/// </summary>
public class SerializeReportConfigRequest
{
    public ReportConfigDefinition Config { get; set; } = new();
    public ReportConfigFormat Format { get; set; } = ReportConfigFormat.Yaml;
}

/// <summary>
/// Response from serializing a report configuration
/// </summary>
public class SerializeReportConfigResponse
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Request to execute reports with a given configuration (event-based)
/// </summary>
public class ExecuteReportsRequest
{
    public string? OperationId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public ReportConfigDefinition Config { get; set; } = new();
    public ReportVerbosity Verbosity { get; set; } = ReportVerbosity.Basic;
    public ReportOutputFormat? Format { get; set; }
    public bool GenerateFile { get; set; }
}

/// <summary>
/// Acknowledgment when starting report execution
/// </summary>
public class ExecuteReportsAcknowledgment
{
    public string OperationId { get; set; } = string.Empty;
    public bool Started { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Progress event during report execution
/// </summary>
public class ReportProgressEvent
{
    public string OperationId { get; set; } = string.Empty;
    public int CurrentReport { get; set; }
    public int TotalReports { get; set; }
    public string? CurrentReportName { get; set; }
    public string Phase { get; set; } = "starting"; // starting, executing, generating-output, complete
    public int Percent { get; set; }
}

/// <summary>
/// Completion event with report execution results
/// </summary>
public class ReportCompletionEvent
{
    public string OperationId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public ReportSummary? Summary { get; set; }
    public List<ReportExecutionResult>? Reports { get; set; }
    public string? OutputContent { get; set; } // Serialized file content if generateFile was true
    public ReportOutputFormat? OutputFormat { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Simple component result without layer details (used internally for queries)
/// </summary>
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
