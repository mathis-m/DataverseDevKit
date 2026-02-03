using System.Text;
using Ddk.SolutionLayerAnalyzer.DTOs;

namespace Ddk.SolutionLayerAnalyzer.Services;

/// <summary>
/// Helper class for CSV serialization
/// </summary>
public static class CsvHelper
{
    /// <summary>
    /// Convert report output to CSV format
    /// </summary>
    public static string SerializeReportOutput(ReportOutput reportOutput, ReportVerbosity verbosity)
    {
        var sb = new StringBuilder();

        // Add summary section
        sb.AppendLine("# SUMMARY");
        sb.AppendLine($"Generated At,{EscapeCsv(reportOutput.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))}");
        sb.AppendLine($"Connection ID,{EscapeCsv(reportOutput.ConnectionId)}");
        sb.AppendLine($"Verbosity,{reportOutput.Verbosity}");
        sb.AppendLine($"Total Reports,{reportOutput.Summary.TotalReports}");
        sb.AppendLine($"Critical Findings,{reportOutput.Summary.CriticalFindings}");
        sb.AppendLine($"Warning Findings,{reportOutput.Summary.WarningFindings}");
        sb.AppendLine($"Informational Findings,{reportOutput.Summary.InformationalFindings}");
        sb.AppendLine($"Total Components,{reportOutput.Summary.TotalComponents}");
        sb.AppendLine();

        // Add detailed findings
        sb.AppendLine("# DETAILED FINDINGS");
        
        if (verbosity == ReportVerbosity.Basic)
        {
            // Basic format: Report, Group, Severity, Action, Component info
            sb.AppendLine("Report Name,Group,Severity,Recommended Action,Component ID,Component Type,Logical Name,Display Name,Solutions,Make Portal URL");
            
            foreach (var report in reportOutput.Reports)
            {
                foreach (var component in report.Components)
                {
                    sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},{EscapeCsv(string.Join("; ", component.Solutions ?? new List<string>()))},{EscapeCsv(component.MakePortalUrl)}");
                }
            }
        }
        else if (verbosity == ReportVerbosity.Medium)
        {
            // Medium format: Adds layer and changed attributes column
            sb.AppendLine("Report Name,Group,Severity,Recommended Action,Component ID,Component Type,Logical Name,Display Name,Solution,Layer Ordinal,Changed Attributes,Make Portal URL");
            
            foreach (var report in reportOutput.Reports)
            {
                foreach (var component in report.Components)
                {
                    if (component.Layers != null && component.Layers.Any())
                    {
                        foreach (var layer in component.Layers)
                        {
                            var changedAttrs = layer.ChangedAttributes != null 
                                ? string.Join("; ", layer.ChangedAttributes.Select(a => a.AttributeName))
                                : string.Empty;
                            
                            sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},{EscapeCsv(layer.SolutionName)},{layer.Ordinal},{EscapeCsv(changedAttrs)},{EscapeCsv(component.MakePortalUrl)}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},,,{EscapeCsv(component.MakePortalUrl)}");
                    }
                }
            }
        }
        else // Verbose
        {
            // Verbose format: One row per attribute change
            sb.AppendLine("Report Name,Group,Severity,Recommended Action,Component ID,Component Type,Logical Name,Display Name,Solution,Layer Ordinal,Attribute Name,Old Value,New Value,Make Portal URL");
            
            foreach (var report in reportOutput.Reports)
            {
                foreach (var component in report.Components)
                {
                    if (component.Layers != null && component.Layers.Any())
                    {
                        foreach (var layer in component.Layers)
                        {
                            if (layer.ChangedAttributes != null && layer.ChangedAttributes.Any())
                            {
                                foreach (var attr in layer.ChangedAttributes)
                                {
                                    sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},{EscapeCsv(layer.SolutionName)},{layer.Ordinal},{EscapeCsv(attr.AttributeName)},{EscapeCsv(attr.OldValue)},{EscapeCsv(attr.NewValue)},{EscapeCsv(component.MakePortalUrl)}");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},{EscapeCsv(layer.SolutionName)},{layer.Ordinal},,,{EscapeCsv(component.MakePortalUrl)}");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{EscapeCsv(report.Name)},{EscapeCsv(report.Group)},{report.Severity},{EscapeCsv(report.RecommendedAction)},{EscapeCsv(component.ComponentId)},{component.ComponentType},{EscapeCsv(component.LogicalName)},{EscapeCsv(component.DisplayName)},,,,,,{EscapeCsv(component.MakePortalUrl)}");
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escape a CSV field value
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // If the value contains comma, quote, or newline, wrap it in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
