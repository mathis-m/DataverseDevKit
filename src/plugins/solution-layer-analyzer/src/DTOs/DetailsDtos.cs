namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the details command.
/// </summary>
public sealed record DetailsRequest
{
    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public Guid ComponentId { get; init; }
}

/// <summary>
/// Response for the details command.
/// </summary>
public sealed record DetailsResponse
{
    /// <summary>
    /// Gets the layers.
    /// </summary>
    public List<LayerDetail> Layers { get; init; } = new();
}

/// <summary>
/// Layer detail.
/// </summary>
public sealed record LayerDetail
{
    /// <summary>
    /// Gets the solution name.
    /// </summary>
    public string SolutionName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the publisher.
    /// </summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this layer is managed.
    /// </summary>
    public bool IsManaged { get; init; }

    /// <summary>
    /// Gets the version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets when the layer was created.
    /// </summary>
    public DateTimeOffset? CreatedOn { get; init; }

    /// <summary>
    /// Gets the ordinal position.
    /// </summary>
    public int Ordinal { get; init; }
}
