using System.Text.Json.Serialization;

namespace Ddk.SolutionLayerAnalyzer.Models;

/// <summary>
/// Severity levels for reports
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportSeverity
{
    /// <summary>
    /// Informational - no immediate action required
    /// </summary>
    Information = 0,
    
    /// <summary>
    /// Warning - should be reviewed and potentially addressed
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Critical - requires immediate attention
    /// </summary>
    Critical = 2
}
