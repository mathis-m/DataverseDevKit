namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a group for organizing reports
/// </summary>
public class ReportGroup
{
    /// <summary>
    /// Unique identifier for the group
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Connection ID / D365 environment identifier
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the group
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Display order for sorting groups
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// When this group was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this group was last modified
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// Navigation property for reports in this group
    /// </summary>
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
