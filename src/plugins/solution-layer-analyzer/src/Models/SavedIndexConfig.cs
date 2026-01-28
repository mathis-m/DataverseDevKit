namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a saved indexing configuration that can be loaded later
/// </summary>
public class SavedIndexConfig
{
    public int Id { get; set; }
    
    /// <summary>
    /// User-provided name for this configuration
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Connection ID / D365 environment identifier
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Comma-separated list of source solutions
    /// </summary>
    public string SourceSolutions { get; set; } = string.Empty;
    
    /// <summary>
    /// Comma-separated list of target solutions
    /// </summary>
    public string TargetSolutions { get; set; } = string.Empty;
    
    /// <summary>
    /// Comma-separated list of component types to include
    /// </summary>
    public string ComponentTypes { get; set; } = string.Empty;
    
    /// <summary>
    /// Payload mode: lazy or eager
    /// </summary>
    public string PayloadMode { get; set; } = "lazy";
    
    /// <summary>
    /// Hash of (ConnectionId, SourceSolutions, TargetSolutions, ComponentTypes) for matching
    /// </summary>
    public string ConfigHash { get; set; } = string.Empty;
    
    /// <summary>
    /// When this configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this configuration was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
