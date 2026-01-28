using System.Text.Json.Serialization;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for comprehensive analytics data
/// </summary>
public class GetAnalyticsRequest
{
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
}

/// <summary>
/// Comprehensive analytics response with all precomputed data
/// </summary>
public class GetAnalyticsResponse
{
    [JsonPropertyName("solutionOverlaps")]
    public SolutionOverlapMatrix SolutionOverlaps { get; set; } = new();
    
    [JsonPropertyName("componentRisks")]
    public List<ComponentRiskSummary> ComponentRisks { get; set; } = new();
    
    [JsonPropertyName("violations")]
    public List<ViolationItem> Violations { get; set; } = new();
    
    [JsonPropertyName("solutionMetrics")]
    public List<SolutionMetrics> SolutionMetrics { get; set; } = new();
    
    [JsonPropertyName("networkData")]
    public NetworkGraphData NetworkData { get; set; } = new();
    
    [JsonPropertyName("hierarchyData")]
    public HierarchyData HierarchyData { get; set; } = new();
    
    [JsonPropertyName("chordData")]
    public ChordDiagramData ChordData { get; set; } = new();
    
    [JsonPropertyName("upSetData")]
    public UpSetPlotData UpSetData { get; set; } = new();
}

/// <summary>
/// Solution-to-solution overlap matrix
/// </summary>
public class SolutionOverlapMatrix
{
    /// <summary>
    /// Matrix: solution1 -> solution2 -> overlap count
    /// </summary>
    [JsonPropertyName("matrix")]
    public Dictionary<string, Dictionary<string, int>> Matrix { get; set; } = new();
    
    /// <summary>
    /// Detailed overlaps by component type
    /// </summary>
    [JsonPropertyName("detailedOverlaps")]
    public List<SolutionOverlapDetail> DetailedOverlaps { get; set; } = new();
}

public class SolutionOverlapDetail
{
    [JsonPropertyName("solution1")]
    public string Solution1 { get; set; } = string.Empty;
    
    [JsonPropertyName("solution2")]
    public string Solution2 { get; set; } = string.Empty;
    
    [JsonPropertyName("totalOverlap")]
    public int TotalOverlap { get; set; }
    
    [JsonPropertyName("managedOverlap")]
    public int ManagedOverlap { get; set; }
    
    [JsonPropertyName("unmanagedOverlap")]
    public int UnmanagedOverlap { get; set; }
    
    [JsonPropertyName("componentTypeBreakdown")]
    public Dictionary<string, int> ComponentTypeBreakdown { get; set; } = new();
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "normal"; // "low", "normal", "high", "critical"
}

/// <summary>
/// Component risk assessment
/// </summary>
public class ComponentRiskSummary
{
    [JsonPropertyName("componentId")]
    public string ComponentId { get; set; } = string.Empty;
    
    [JsonPropertyName("componentType")]
    public string ComponentType { get; set; } = string.Empty;
    
    [JsonPropertyName("logicalName")]
    public string LogicalName { get; set; } = string.Empty;
    
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
    
    [JsonPropertyName("riskScore")]
    public int RiskScore { get; set; } // 0-100
    
    [JsonPropertyName("layerDepth")]
    public int LayerDepth { get; set; }
    
    [JsonPropertyName("topmostSolution")]
    public string TopmostSolution { get; set; } = string.Empty;
    
    [JsonPropertyName("baseSolution")]
    public string BaseSolution { get; set; } = string.Empty;
    
    [JsonPropertyName("hasUnmanagedOverride")]
    public bool HasUnmanagedOverride { get; set; }
    
    [JsonPropertyName("violationFlags")]
    public List<string> ViolationFlags { get; set; } = new();
    
    [JsonPropertyName("modifyingSolutions")]
    public List<string> ModifyingSolutions { get; set; } = new();
}

/// <summary>
/// Detected violation
/// </summary>
public class ViolationItem
{
    [JsonPropertyName("violationId")]
    public string ViolationId { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "unmanaged_override", "forbidden_layering", "excessive_depth"
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "medium"; // "low", "medium", "high", "critical"
    
    [JsonPropertyName("componentId")]
    public string ComponentId { get; set; } = string.Empty;
    
    [JsonPropertyName("componentName")]
    public string ComponentName { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("affectedSolutions")]
    public List<string> AffectedSolutions { get; set; } = new();
}

/// <summary>
/// Per-solution metrics
/// </summary>
public class SolutionMetrics
{
    [JsonPropertyName("solutionName")]
    public string SolutionName { get; set; } = string.Empty;
    
    [JsonPropertyName("isManaged")]
    public bool IsManaged { get; set; }
    
    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = string.Empty;
    
    [JsonPropertyName("totalLayers")]
    public int TotalLayers { get; set; }
    
    [JsonPropertyName("unmanagedLayers")]
    public int UnmanagedLayers { get; set; }
    
    [JsonPropertyName("componentsModified")]
    public int ComponentsModified { get; set; }
    
    [JsonPropertyName("componentTypeBreakdown")]
    public Dictionary<string, int> ComponentTypeBreakdown { get; set; } = new();
    
    [JsonPropertyName("violationCount")]
    public int ViolationCount { get; set; }
    
    [JsonPropertyName("overlapsWith")]
    public List<string> OverlapsWith { get; set; } = new();
}

/// <summary>
/// Network graph structure for force-directed layout
/// </summary>
public class NetworkGraphData
{
    [JsonPropertyName("nodes")]
    public List<NetworkNode> Nodes { get; set; } = new();
    
    [JsonPropertyName("links")]
    public List<NetworkLink> Links { get; set; } = new();
}

public class NetworkNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "solution"; // "solution" or "component"
    
    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty; // For color grouping
    
    [JsonPropertyName("size")]
    public int Size { get; set; } // Visual size based on importance
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class NetworkLink
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
    
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public int Value { get; set; } // Strength/weight
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "modifies"; // "modifies", "depends_on", etc.
}

/// <summary>
/// Hierarchical tree structure
/// </summary>
public class HierarchyData
{
    [JsonPropertyName("root")]
    public HierarchyNode Root { get; set; } = new();
}

public class HierarchyNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = "solution";
    
    [JsonPropertyName("children")]
    public List<HierarchyNode> Children { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Chord diagram data
/// </summary>
public class ChordDiagramData
{
    [JsonPropertyName("solutions")]
    public List<string> Solutions { get; set; } = new();
    
    [JsonPropertyName("matrix")]
    public List<List<int>> Matrix { get; set; } = new(); // Square matrix of overlaps
    
    [JsonPropertyName("details")]
    public Dictionary<string, ChordDetail> Details { get; set; } = new();
}

public class ChordDetail
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
    
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("components")]
    public List<string> Components { get; set; } = new();
}

/// <summary>
/// UpSet plot data for set intersections
/// </summary>
public class UpSetPlotData
{
    [JsonPropertyName("sets")]
    public List<string> Sets { get; set; } = new(); // Solution names
    
    [JsonPropertyName("intersections")]
    public List<SetIntersection> Intersections { get; set; } = new();
}

public class SetIntersection
{
    [JsonPropertyName("sets")]
    public List<string> Sets { get; set; } = new(); // Solutions in this intersection
    
    [JsonPropertyName("size")]
    public int Size { get; set; } // Number of components
    
    [JsonPropertyName("components")]
    public List<string> Components { get; set; } = new();
    
    [JsonPropertyName("degree")]
    public int Degree { get; set; } // Number of sets in intersection
}
