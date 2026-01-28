using System.Text.Json.Serialization;

namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request to fetch all available solutions from Dataverse.
/// </summary>
public class FetchSolutionsRequest
{
    /// <summary>
    /// Connection ID for the Dataverse environment.
    /// </summary>
    [JsonPropertyName("connectionId")]
    public string? ConnectionId { get; set; }
}

/// <summary>
/// Response containing list of available solutions.
/// </summary>
public class FetchSolutionsResponse
{
    /// <summary>
    /// List of solutions available in the Dataverse environment.
    /// </summary>
    [JsonPropertyName("solutions")]
    public List<SolutionInfo> Solutions { get; set; } = new();
}

/// <summary>
/// Information about a single solution.
/// </summary>
public class SolutionInfo
{
    /// <summary>
    /// Unique name (technical name) of the solution.
    /// </summary>
    [JsonPropertyName("uniqueName")]
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>
    /// Display name (friendly name) of the solution.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Solution version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Whether the solution is managed.
    /// </summary>
    [JsonPropertyName("isManaged")]
    public bool IsManaged { get; set; }

    /// <summary>
    /// Publisher name.
    /// </summary>
    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }
}

/// <summary>
/// Response containing list of all supported component types.
/// </summary>
public class GetComponentTypesResponse
{
    /// <summary>
    /// List of supported component types.
    /// </summary>
    [JsonPropertyName("componentTypes")]
    public List<ComponentTypeInfo> ComponentTypes { get; set; } = new();
}

/// <summary>
/// Information about a component type.
/// </summary>
public class ComponentTypeInfo
{
    /// <summary>
    /// Technical name of the component type.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Component type code.
    /// </summary>
    [JsonPropertyName("typeCode")]
    public int TypeCode { get; set; }
}
