namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a saved filter configuration that can be loaded later
/// </summary>
public class SavedFilterConfig
{
    public int Id { get; set; }
    
    /// <summary>
    /// User-provided name for this configuration
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Connection ID / D365 environment identifier (for grouping/prioritization)
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Hash of originating index config for smart matching
    /// </summary>
    public string? OriginatingIndexHash { get; set; }
    
    /// <summary>
    /// JSON serialized FilterNode AST
    /// </summary>
    public string? FilterJson { get; set; }
    
    /// <summary>
    /// When this configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this configuration was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
