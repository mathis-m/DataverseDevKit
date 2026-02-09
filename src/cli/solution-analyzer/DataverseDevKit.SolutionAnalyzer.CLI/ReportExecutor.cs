using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Ddk.SolutionLayerAnalyzer;
using Ddk.SolutionLayerAnalyzer.Models;
using Ddk.SolutionLayerAnalyzer.DTOs;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Executes reports from configuration for CI/CD monitoring by directly calling the plugin.
/// Uses stateless approach - config file content is sent to plugin for parsing.
/// </summary>
public class ReportExecutor
{
    private readonly FileInfo _configFile;
    private readonly Uri _environmentUrl;
    private readonly string? _connectionString;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _tenantId;
    private readonly DirectoryInfo _outputDirectory;
    private readonly string _format;
    private readonly string _verbosity;
    private readonly string? _failOnSeverity;
    private readonly int? _maxFindings;
    private readonly ILogger _consoleLogger;
    private readonly string _pluginLogPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ReportExecutor(
        FileInfo configFile,
        Uri environmentUrl,
        string? connectionString,
        string? clientId,
        string? clientSecret,
        string? tenantId,
        LogLevel consoleLogLevel,
        DirectoryInfo outputDirectory,
        string format,
        string? failOnSeverity,
        int? maxFindings,
        string verbosity = "basic")
    {
        _configFile = configFile;
        _environmentUrl = environmentUrl;
        _connectionString = connectionString;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
        _outputDirectory = outputDirectory;
        _format = format;
        _verbosity = verbosity;
        _failOnSeverity = failOnSeverity;
        _maxFindings = maxFindings;

        // Ensure output directory exists
        if (!_outputDirectory.Exists)
        {
            _outputDirectory.Create();
        }

        // Setup console logging for CLI
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(consoleLogLevel);
            builder.AddConsole();
        });
        _consoleLogger = loggerFactory.CreateLogger<ReportExecutor>();

        // Setup file logging path for plugin (to sandbox plugin logs)
        _pluginLogPath = Path.Combine(_outputDirectory.FullName, $"plugin-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
    }

    /// <summary>
    /// Execute all reports from configuration and return exit code.
    /// Uses stateless approach: reads config file, sends to plugin for parsing, executes reports.
    /// </summary>
    public async Task<int> ExecuteReportsAsync(CancellationToken cancellationToken)
    {
        _consoleLogger.LogInformation("Loading configuration from: {ConfigFile}", _configFile.FullName);
        
        try
        {
            // Read config file content as string
            var configContent = await File.ReadAllTextAsync(_configFile.FullName, cancellationToken);
            
            // Create plugin instance and context
            var plugin = new SolutionLayerAnalyzerPlugin();
            var pluginLogger = CreatePluginLogger();
            var serviceClientFactory = new CliServiceClientFactory(
                _environmentUrl.AbsoluteUri,
                _connectionString, 
                _clientId, 
                _clientSecret, 
                _tenantId
            );
            
            var pluginContext = new CliPluginContext(
                pluginLogger,
                _outputDirectory.FullName,
                serviceClientFactory
            );

            _consoleLogger.LogDebug("Initializing plugin");
            await plugin.InitializeAsync(pluginContext, cancellationToken);

            try
            {
                // Parse config via plugin (stateless)
                _consoleLogger.LogInformation("Parsing report configuration");
                var parseRequest = new ParseReportConfigRequest
                {
                    Content = configContent,
                    Format = DetectConfigFormat(_configFile.Name)
                };
                
                var parsePayload = JsonSerializer.Serialize(parseRequest, JsonOptions);
                var parseResultElement = await plugin.ExecuteAsync("parseReportConfig", parsePayload, cancellationToken);
                var parseResult = JsonSerializer.Deserialize<ParseReportConfigResponse>(
                    parseResultElement.GetRawText(), JsonOptions);

                if (parseResult == null || parseResult.Errors.Count > 0)
                {
                    foreach (var error in parseResult?.Errors ?? new List<string> { "Unknown parse error" })
                    {
                        _consoleLogger.LogError("Config parse error: {Error}", error);
                    }
                    return 2;
                }

                var config = parseResult.Config;
                var totalReports = config.ReportGroups.Sum(g => g.Reports.Count) + config.UngroupedReports.Count;
                _consoleLogger.LogInformation("Loaded {ReportCount} reports from {GroupCount} groups", 
                    totalReports, config.ReportGroups.Count);

                // Build index request from config
                _consoleLogger.LogInformation("Building index for {SourceCount} sources and {TargetCount} targets",
                    config.SourceSolutions.Count, config.TargetSolutions.Count);
                
                var indexRequest = new IndexRequest
                {
                    ConnectionId = _environmentUrl.AbsoluteUri,
                    SourceSolutions = config.SourceSolutions,
                    TargetSolutions = config.TargetSolutions,
                    IncludeComponentTypes = config.ComponentTypes?.Select(c => c.ToString(CultureInfo.InvariantCulture)).ToList() ?? new List<string>(),
                    PayloadMode = "lazy"
                };

                var indexPayload = JsonSerializer.Serialize(indexRequest, JsonOptions);
                await plugin.ExecuteAsync("index", indexPayload, cancellationToken);

                // Wait for index completion (poll for completion event or check metadata)
                _consoleLogger.LogInformation("Waiting for index to complete...");
                await WaitForIndexCompletionAsync(plugin, _environmentUrl.AbsoluteUri, cancellationToken);

                // Execute reports via stateless executeReports command
                _consoleLogger.LogInformation("Executing reports...");
                var verbosity = ParseVerbosity(_verbosity);
                var format = ParseOutputFormat(_format);
                
                var executeRequest = new ExecuteReportsRequest
                {
                    OperationId = Guid.NewGuid().ToString("N"),
                    ConnectionId = _environmentUrl.AbsoluteUri,
                    Config = config,
                    Verbosity = verbosity,
                    Format = format,
                    GenerateFile = true
                };

                var executePayload = JsonSerializer.Serialize(executeRequest, JsonOptions);
                var executeResultElement = await plugin.ExecuteAsync("executeReports", executePayload, cancellationToken);
                var ack = JsonSerializer.Deserialize<ExecuteReportsAcknowledgment>(
                    executeResultElement.GetRawText(), JsonOptions);

                if (ack == null || !ack.Started)
                {
                    _consoleLogger.LogError("Failed to start report execution: {Error}", ack?.ErrorMessage ?? "Unknown error");
                    return 2;
                }

                // Poll for completion via plugin context events
                var completionResult = await WaitForReportCompletionAsync(pluginContext, ack.OperationId, cancellationToken);

                if (!completionResult.Success)
                {
                    _consoleLogger.LogError("Report execution failed: {Error}", completionResult.ErrorMessage);
                    return 2;
                }

                // Extract results
                var summary = completionResult.Summary;
                var criticalCount = summary?.CriticalFindings ?? 0;
                var warningCount = summary?.WarningFindings ?? 0;
                var infoCount = summary?.InformationalFindings ?? 0;

                // Save output content
                var reportFileName = $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{GetFileExtension(_format)}";
                var reportPath = Path.Combine(_outputDirectory.FullName, reportFileName);
                
                if (!string.IsNullOrEmpty(completionResult.OutputContent))
                {
                    await File.WriteAllTextAsync(reportPath, completionResult.OutputContent, cancellationToken);
                    _consoleLogger.LogInformation("Report saved to: {ReportPath}", reportPath);
                }
                
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

    private async Task WaitForIndexCompletionAsync(
        SolutionLayerAnalyzerPlugin plugin, 
        string connectionId, 
        CancellationToken cancellationToken)
    {
        // Poll index metadata until indexing is complete
        var timeout = TimeSpan.FromMinutes(30);
        var checkInterval = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadataRequest = new { ConnectionId = connectionId };
            var metadataPayload = JsonSerializer.Serialize(metadataRequest, JsonOptions);
            
            try
            {
                var resultElement = await plugin.ExecuteAsync("getIndexMetadata", metadataPayload, cancellationToken);
                var metadata = JsonSerializer.Deserialize<JsonElement>(resultElement.GetRawText(), JsonOptions);
                
                if (metadata.TryGetProperty("componentCount", out var countProp) && countProp.GetInt32() > 0)
                {
                    _consoleLogger.LogInformation("Index complete: {ComponentCount} components indexed", countProp.GetInt32());
                    return;
                }
            }
            catch
            {
                // Index might not be ready yet, continue waiting
            }

            await Task.Delay(checkInterval, cancellationToken);
        }

        throw new TimeoutException("Index operation timed out");
    }

    private async Task<ReportCompletionEvent> WaitForReportCompletionAsync(
        CliPluginContext pluginContext,
        string operationId,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(30);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if we have a completion event
            if (pluginContext.TryGetCompletionEvent(operationId, out var completionEvent))
            {
                return completionEvent;
            }

            // Log progress if available
            if (pluginContext.TryGetProgressEvent(operationId, out var progressEvent))
            {
                _consoleLogger.LogInformation(
                    "Progress: {Phase} - {Current}/{Total} ({Percent}%)", 
                    progressEvent.Phase,
                    progressEvent.CurrentReport,
                    progressEvent.TotalReports,
                    progressEvent.Percent);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException("Report execution timed out");
    }

    private static ReportConfigFormat? DetectConfigFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToUpperInvariant();
        return extension switch
        {
            ".JSON" => ReportConfigFormat.Json,
            ".YAML" or ".YML" => ReportConfigFormat.Yaml,
            ".XML" => ReportConfigFormat.Xml,
            _ => null // Let plugin auto-detect
        };
    }

    private static ReportVerbosity ParseVerbosity(string verbosity)
    {
        return verbosity.ToUpperInvariant() switch
        {
            "MEDIUM" => ReportVerbosity.Medium,
            "VERBOSE" => ReportVerbosity.Verbose,
            _ => ReportVerbosity.Basic
        };
    }

    private static ReportOutputFormat ParseOutputFormat(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "JSON" => ReportOutputFormat.Json,
            "CSV" => ReportOutputFormat.Csv,
            _ => ReportOutputFormat.Yaml
        };
    }

    private ILogger CreatePluginLogger()
    {
        // Create file-only logger for plugin to sandbox its logs
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new FileLoggerProvider(_pluginLogPath, LogLevel.Debug));
        });
        
        return loggerFactory.CreateLogger("Plugin");
    }

#pragma warning disable CA1303
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
#pragma warning restore CA1303

    private int DetermineExitCode(int criticalCount, int warningCount, int infoCount)
    {
        var totalFindings = criticalCount + warningCount + infoCount;

        // Check max findings threshold first
        if (totalFindings > _maxFindings)
        {
            _consoleLogger.LogWarning("Total findings ({Total}) exceeds threshold ({Max})", 
                totalFindings, _maxFindings.Value);
            return 1;
        }

        // Check severity-based failure
        if (string.IsNullOrEmpty(_failOnSeverity))
        {
            return 0;
        }

#pragma warning disable CA1308
        var failSeverity = _failOnSeverity.ToLowerInvariant();
#pragma warning restore CA1308
            
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

        // No failures - success
        return 0;
    }

    private static string GetFileExtension(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "JSON" => "json",
            "CSV" => "csv",
            _ => "yaml"
        };
    }
}
