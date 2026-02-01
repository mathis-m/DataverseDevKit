namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents the global configuration for the analyzer including settings and reports
/// </summary>
public class AnalyzerConfig
{
    /// <summary>
    /// Source solutions to analyze
    /// </summary>
    public List<string> SourceSolutions { get; set; } = new();
    
    /// <summary>
    /// Target solutions to compare against
    /// </summary>
    public List<string> TargetSolutions { get; set; } = new();
    
    /// <summary>
    /// Component types to include in analysis (e.g., "Entity", "SystemForm", "SavedQuery")
    /// </summary>
    public List<string>? ComponentTypes { get; set; }
    
    /// <summary>
    /// Report groups and their reports
    /// </summary>
    public List<ConfigReportGroup> ReportGroups { get; set; } = new();
    
    /// <summary>
    /// Reports that are not in any group
    /// </summary>
    public List<ConfigReport> UngroupedReports { get; set; } = new();
}

/// <summary>
/// Report group for XML serialization
/// </summary>
public class ConfigReportGroup
{
    /// <summary>
    /// Name of the group
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Reports in this group
    /// </summary>
    public List<ConfigReport> Reports { get; set; } = new();
}

/// <summary>
/// Report configuration for XML serialization
/// </summary>
public class ConfigReport
{
    /// <summary>
    /// Name of the report
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Severity level
    /// </summary>
    public ReportSeverity Severity { get; set; } = ReportSeverity.Information;
    
    /// <summary>
    /// Recommended action
    /// </summary>
    public string? RecommendedAction { get; set; }
    
    /// <summary>
    /// Filter query as JSON
    /// </summary>
    public string QueryJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }
}
