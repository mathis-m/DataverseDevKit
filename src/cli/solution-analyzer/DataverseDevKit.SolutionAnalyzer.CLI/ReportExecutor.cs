using System.Text.Json;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Ddk.SolutionLayerAnalyzer;
using Ddk.SolutionLayerAnalyzer.Models;
using Ddk.SolutionLayerAnalyzer.DTOs;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Executes reports from configuration for CI/CD monitoring by directly calling the plugin
/// </summary>
public class ReportExecutor
{
    private readonly FileInfo _configFile;
    private readonly string _environmentUrl;
    private readonly string? _connectionString;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _tenantId;
    private readonly DirectoryInfo _outputDirectory;
    private readonly string _format;
    private readonly string? _failOnSeverity;
    private readonly int? _maxFindings;
    private readonly ILogger _consoleLogger;
    private readonly string _pluginLogPath;

    public ReportExecutor(
        FileInfo configFile,
        string environmentUrl,
        string? connectionString,
        string? clientId,
        string? clientSecret,
        string? tenantId,
        LogLevel consoleLogLevel,
        DirectoryInfo outputDirectory,
        string format,
        string? failOnSeverity,
        int? maxFindings)
    {
        _configFile = configFile;
        _environmentUrl = environmentUrl;
        _connectionString = connectionString;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
        _outputDirectory = outputDirectory;
        _format = format;
        _failOnSeverity = failOnSeverity;
        _maxFindings = maxFindings;

        // Ensure output directory exists
        if (!_outputDirectory.Exists)
        {
            _outputDirectory.Create();
        }

        // Setup console logging for CLI
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(consoleLogLevel);
            builder.AddConsole();
        });
        _consoleLogger = loggerFactory.CreateLogger<ReportExecutor>();

        // Setup file logging path for plugin (to sandbox plugin logs)
        _pluginLogPath = Path.Combine(_outputDirectory.FullName, $"plugin-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
    }

    /// <summary>
    /// Execute all reports from configuration and return exit code
    /// </summary>
    public async Task<int> ExecuteReportsAsync(CancellationToken cancellationToken)
    {
        _consoleLogger.LogInformation("Loading configuration from: {ConfigFile}", _configFile.FullName);
        
        try
        {
            // Load configuration
            var config = await LoadConfigurationAsync(cancellationToken);
            
            var totalReports = config.ReportGroups.Sum(g => g.Reports.Count) + config.UngroupedReports.Count;
            _consoleLogger.LogInformation("Loaded {ReportCount} reports from {GroupCount} groups", 
                totalReports, config.ReportGroups.Count);

            // Create plugin instance and context
            var plugin = new SolutionLayerAnalyzerPlugin();
            var pluginLogger = CreatePluginLogger();
            var serviceClientFactory = new CliServiceClientFactory(
                _environmentUrl, 
                _connectionString, 
                _clientId, 
                _clientSecret, 
                _tenantId);
            
            var pluginContext = new CliPluginContext(
                pluginLogger,
                _outputDirectory.FullName,
                serviceClientFactory);

            _consoleLogger.LogDebug("Initializing plugin");
            await plugin.InitializeAsync(pluginContext, cancellationToken);

            try
            {
                // First, ensure we have an index
                _consoleLogger.LogInformation("Checking index status");
                
                // Build index request from config
                var indexRequest = new IndexRequest
                {
                    ConnectionId = _environmentUrl,
                    SourceSolutions = config.SourceSolutions,
                    TargetSolutions = config.TargetSolutions,
                    ComponentTypes = config.ComponentTypes ?? new List<int>(),
                    PayloadMode = "lazy"
                };

                var indexPayload = JsonSerializer.Serialize(indexRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _consoleLogger.LogInformation("Building index for {SourceCount} sources and {TargetCount} targets",
                    config.SourceSolutions.Count, config.TargetSolutions.Count);
                
                await plugin.ExecuteAsync("index", indexPayload, cancellationToken);

                // Execute all reports and collect results
                var criticalCount = 0;
                var warningCount = 0;
                var infoCount = 0;
                var allComponents = new List<ReportComponentResult>();

                foreach (var group in config.ReportGroups)
                {
                    _consoleLogger.LogInformation("Executing group: {GroupName}", group.Name);
                    
                    foreach (var report in group.Reports)
                    {
                        _consoleLogger.LogDebug("  - {ReportName} (Severity: {Severity})", 
                            report.Name, report.Severity);
                        
                        // Execute report via plugin
                        var reportResult = await ExecuteReportViaPlugin(
                            plugin, 
                            report, 
                            _environmentUrl, 
                            cancellationToken);

                        // Accumulate findings
                        switch (report.Severity)
                        {
                            case ReportSeverity.Critical:
                                criticalCount += reportResult.TotalMatches;
                                break;
                            case ReportSeverity.Warning:
                                warningCount += reportResult.TotalMatches;
                                break;
                            case ReportSeverity.Information:
                                infoCount += reportResult.TotalMatches;
                                break;
                        }

                        allComponents.AddRange(reportResult.Components);
                    }
                }

                // Generate output report
                var reportFileName = $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{GetFileExtension(_format)}";
                var reportPath = Path.Combine(_outputDirectory.FullName, reportFileName);
                
                await GenerateOutputReportAsync(config, allComponents, reportPath, cancellationToken);
                
                _consoleLogger.LogInformation("Report saved to: {ReportPath}", reportPath);
                _consoleLogger.LogInformation("Plugin logs: {PluginLog}", _pluginLogPath);

                // Print summary
                PrintSummary(totalReports, criticalCount, warningCount, infoCount, reportPath);

                // Determine exit code
                var exitCode = DetermineExitCode(criticalCount, warningCount, infoCount);
                
                PrintExitStatus(exitCode, criticalCount, warningCount, infoCount);

                return exitCode;
            }
            finally
            {
                await plugin.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _consoleLogger.LogError(ex, "Report execution failed");
            PrintErrorSummary(ex);
            return 2; // Error exit code
        }
    }

    private async Task<ExecuteReportResponse> ExecuteReportViaPlugin(
        SolutionLayerAnalyzerPlugin plugin,
        ConfigReport report,
        string connectionId,
        CancellationToken cancellationToken)
    {
        // Create a temporary saved report to execute
        var saveRequest = new SaveReportRequest
        {
            ConnectionId = connectionId,
            Name = report.Name,
            Description = report.Description,
            Severity = report.Severity,
            RecommendedAction = report.RecommendedAction,
            QueryJson = report.QueryJson
        };

        var savePayload = JsonSerializer.Serialize(saveRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var savedReportElement = await plugin.ExecuteAsync("saveReport", savePayload, cancellationToken);
        var savedReport = JsonSerializer.Deserialize<ReportDto>(
            savedReportElement.GetRawText(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (savedReport == null)
        {
            throw new InvalidOperationException($"Failed to save report: {report.Name}");
        }

        // Execute the report
        var executeRequest = new ExecuteReportRequest
        {
            Id = savedReport.Id,
            ConnectionId = connectionId
        };

        var executePayload = JsonSerializer.Serialize(executeRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var resultElement = await plugin.ExecuteAsync("executeReport", executePayload, cancellationToken);
        var result = JsonSerializer.Deserialize<ExecuteReportResponse>(
            resultElement.GetRawText(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (result == null)
        {
            throw new InvalidOperationException($"Failed to execute report: {report.Name}");
        }

        // Clean up the temporary report
        var deleteRequest = new DeleteReportRequest
        {
            Id = savedReport.Id,
            ConnectionId = connectionId
        };

        var deletePayload = JsonSerializer.Serialize(deleteRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await plugin.ExecuteAsync("deleteReport", deletePayload, cancellationToken);

        return result;
    }

    private async Task GenerateOutputReportAsync(
        AnalyzerConfig config,
        List<ReportComponentResult> components,
        string outputPath,
        CancellationToken cancellationToken)
    {
        // Create simple output structure
        var output = new
        {
            GeneratedAt = DateTime.UtcNow,
            SourceSolutions = config.SourceSolutions,
            TargetSolutions = config.TargetSolutions,
            TotalComponents = components.Count,
            Components = components
        };

        string content;
        if (_format.ToLowerInvariant() == "json")
        {
            content = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        else if (_format.ToLowerInvariant() == "csv")
        {
            // Simple CSV output
            var lines = new List<string>
            {
                "ComponentId,ComponentType,ComponentTypeName,LogicalName,DisplayName,Solutions"
            };
            
            foreach (var comp in components)
            {
                lines.Add($"{comp.ComponentId},{comp.ComponentType},{comp.ComponentTypeName},{comp.LogicalName},{comp.DisplayName},{string.Join(";", comp.Solutions)}");
            }
            
            content = string.Join(Environment.NewLine, lines);
        }
        else // YAML
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            content = serializer.Serialize(output);
        }

        await File.WriteAllTextAsync(outputPath, content, cancellationToken);
    }

    private ILogger CreatePluginLogger()
    {
        // Create file-only logger for plugin to sandbox its logs
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new FileLoggerProvider(_pluginLogPath, LogLevel.Debug));
        });
        
        return loggerFactory.CreateLogger("Plugin");
    }

    private void PrintSummary(int totalReports, int criticalCount, int warningCount, int infoCount, string reportPath)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  SOLUTION INTEGRITY REPORT SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine($"  Reports Executed:    {totalReports}");
        Console.WriteLine($"  Critical Findings:   {criticalCount}");
        Console.WriteLine($"  Warning Findings:    {warningCount}");
        Console.WriteLine($"  Info Findings:       {infoCount}");
        Console.WriteLine($"  Total Findings:      {criticalCount + warningCount + infoCount}");
        Console.WriteLine("───────────────────────────────────────────────────────────");
        Console.WriteLine($"  Output:              {reportPath}");
        Console.WriteLine($"  Plugin Logs:         {_pluginLogPath}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    private void PrintExitStatus(int exitCode, int criticalCount, int warningCount, int infoCount)
    {
        if (exitCode == 0)
        {
            Console.WriteLine("  Status: ✓ PASSED - Solution integrity check successful");
        }
        else
        {
            Console.WriteLine($"  Status: ✗ FAILED - Exit code {exitCode}");
            if (!string.IsNullOrEmpty(_failOnSeverity))
            {
                Console.WriteLine($"  Reason: Findings with severity '{_failOnSeverity}' or higher detected");
            }
            if (_maxFindings.HasValue)
            {
                Console.WriteLine($"  Reason: Total findings ({criticalCount + warningCount + infoCount}) exceeds threshold ({_maxFindings})");
            }
        }
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    private void PrintErrorSummary(Exception ex)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
        Console.Error.WriteLine("  ✗ EXECUTION FAILED");
        Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
        Console.Error.WriteLine($"  Error: {ex.Message}");
        Console.Error.WriteLine($"  See plugin logs: {_pluginLogPath}");
        Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
        Console.Error.WriteLine();
    }

    private int DetermineExitCode(int criticalCount, int warningCount, int infoCount)
    {
        var totalFindings = criticalCount + warningCount + infoCount;

        // Check max findings threshold first
        if (_maxFindings.HasValue && totalFindings > _maxFindings.Value)
        {
            _consoleLogger.LogWarning("Total findings ({Total}) exceeds threshold ({Max})", 
                totalFindings, _maxFindings.Value);
            return 1;
        }

        // Check severity-based failure
        if (!string.IsNullOrEmpty(_failOnSeverity))
        {
            var failSeverity = _failOnSeverity.ToLowerInvariant();
            
            if (failSeverity == "information" && totalFindings > 0)
            {
                _consoleLogger.LogWarning("Failing on information severity: {Total} findings", totalFindings);
                return 1;
            }
            
            if (failSeverity == "warning" && (warningCount > 0 || criticalCount > 0))
            {
                _consoleLogger.LogWarning("Failing on warning severity: {Warning} warnings, {Critical} critical", 
                    warningCount, criticalCount);
                return 1;
            }
            
            if (failSeverity == "critical" && criticalCount > 0)
            {
                _consoleLogger.LogError("Failing on critical severity: {Critical} critical findings", criticalCount);
                return 1;
            }
        }

        // No failures - success
        return 0;
    }

    private async Task<AnalyzerConfig> LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        if (!_configFile.Exists)
        {
            throw new FileNotFoundException($"Configuration file not found: {_configFile.FullName}");
        }

        var yaml = await File.ReadAllTextAsync(_configFile.FullName, cancellationToken);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        return deserializer.Deserialize<AnalyzerConfig>(yaml);
    }

    private static string GetFileExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => "json",
            "csv" => "csv",
            _ => "yaml"
        };
    }
}
