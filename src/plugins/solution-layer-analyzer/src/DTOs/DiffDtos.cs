namespace Ddk.SolutionLayerAnalyzer.DTOs;

/// <summary>
/// Request for the diff command.
/// </summary>
public sealed record DiffRequest
{
    /// <summary>
    /// Gets the component ID.
    /// </summary>
    public Guid ComponentId { get; init; }

    /// <summary>
    /// Gets the left layer.
    /// </summary>
    public DiffLayer Left { get; init; } = new();

    /// <summary>
    /// Gets the right layer.
    /// </summary>
    public DiffLayer Right { get; init; } = new();
}

/// <summary>
/// Diff layer specification.
/// </summary>
public sealed record DiffLayer
{
    /// <summary>
    /// Gets the solution name.
    /// </summary>
    public string SolutionName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the payload type ("auto", "xml", "json", or "text").
    /// </summary>
    public string PayloadType { get; init; } = "auto";
}

/// <summary>
/// Response for the diff command.
/// </summary>
public sealed record DiffResponse
{
    /// <summary>
    /// Gets the left payload text.
    /// </summary>
    public string LeftText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the right payload text.
    /// </summary>
    public string RightText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the MIME type.
    /// </summary>
    public string Mime { get; init; } = "text/plain";

    /// <summary>
    /// Gets any warnings.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}
