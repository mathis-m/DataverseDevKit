namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Represents a Dataverse solution.
/// </summary>
public sealed class Solution
{
    /// <summary>
    /// Gets or sets the solution ID (primary key).
    /// </summary>
    public Guid SolutionId { get; set; }

    /// <summary>
    /// Gets or sets the unique name.
    /// </summary>
    public string UniqueName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the friendly name.
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the publisher name.
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the solution is managed.
    /// </summary>
    public bool IsManaged { get; set; }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is a source solution.
    /// </summary>
    public bool IsSource { get; set; }

    /// <summary>
    /// Gets or sets whether this is a target solution.
    /// </summary>
    public bool IsTarget { get; set; }
}
