namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a saved report configuration with query and metadata
/// </summary>
public class Report
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Connection ID / D365 environment identifier
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the report
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description of what this report checks
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Group ID this report belongs to (nullable)
    /// </summary>
    public int? GroupId { get; set; }
    
    /// <summary>
    /// Navigation property to the group
    /// </summary>
    public ReportGroup? Group { get; set; }
    
    /// <summary>
    /// Severity level of findings in this report
    /// </summary>
    public ReportSeverity Severity { get; set; } = ReportSeverity.Information;
    
    /// <summary>
    /// Recommended action to take for matched components
    /// </summary>
    public string? RecommendedAction { get; set; }
    
    /// <summary>
    /// JSON serialized FilterNode AST representing the query
    /// </summary>
    public string QueryJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Display order for sorting reports within a group
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Hash of originating index config for validation
    /// </summary>
    public string? OriginatingIndexHash { get; set; }
    
    /// <summary>
    /// When this report was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this report was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// When this report was last executed
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }
}
