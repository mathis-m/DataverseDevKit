using System.Text.Json.Serialization;
using Ddk.SolutionLayerAnalyzer.Filters;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

// ============= Save Index Config =============

public class SaveIndexConfigRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = string.Empty;
    
    [JsonPropertyName("sourceSolutions")]
    public List<string> SourceSolutions { get; set; } = new();
    
    [JsonPropertyName("targetSolutions")]
    public List<string> TargetSolutions { get; set; } = new();
    
    [JsonPropertyName("componentTypes")]
    public List<string> ComponentTypes { get; set; } = new();
    
    [JsonPropertyName("payloadMode")]
    public string PayloadMode { get; set; } = "lazy";
}

public class SaveIndexConfigResponse
{
    [JsonPropertyName("configId")]
    public int ConfigId { get; set; }
    
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; set; } = string.Empty;
}

// ============= Load Index Configs =============

public class LoadIndexConfigsRequest
{
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
}

public class IndexConfigItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = string.Empty;
    
    [JsonPropertyName("sourceSolutions")]
    public List<string> SourceSolutions { get; set; } = new();
    
    [JsonPropertyName("targetSolutions")]
    public List<string> TargetSolutions { get; set; } = new();
    
    [JsonPropertyName("componentTypes")]
    public List<string> ComponentTypes { get; set; } = new();
    
    [JsonPropertyName("payloadMode")]
    public string PayloadMode { get; set; } = "lazy";
    
    [JsonPropertyName("configHash")]
    public string ConfigHash { get; set; } = string.Empty;
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }
    
    [JsonPropertyName("isSameEnvironment")]
    public bool IsSameEnvironment { get; set; }
}

public class LoadIndexConfigsResponse
{
    [JsonPropertyName("configs")]
    public List<IndexConfigItem> Configs { get; set; } = new();
}

// ============= Save Filter Config =============

public class SaveFilterConfigRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
    
    [JsonPropertyName("originatingIndexHash")]
    public string? OriginatingIndexHash { get; set; }
    
    [JsonPropertyName("filter")]
    public FilterNode? Filter { get; set; }
}

public class SaveFilterConfigResponse
{
    [JsonPropertyName("configId")]
    public int ConfigId { get; set; }
}

// ============= Load Filter Configs =============

public class LoadFilterConfigsRequest
{
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
    
    [JsonPropertyName("currentIndexHash")]
    public string? CurrentIndexHash { get; set; }
}

public class FilterConfigItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
    
    [JsonPropertyName("originatingIndexHash")]
    public string? OriginatingIndexHash { get; set; }
    
    [JsonPropertyName("filter")]
    public FilterNode? Filter { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }
    
    [JsonPropertyName("matchesCurrentIndex")]
    public bool MatchesCurrentIndex { get; set; }
    
    [JsonPropertyName("isSameEnvironment")]
    public bool IsSameEnvironment { get; set; }
}

public class LoadFilterConfigsResponse
{
    [JsonPropertyName("configs")]
    public List<FilterConfigItem> Configs { get; set; } = new();
}
