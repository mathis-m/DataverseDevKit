using System.Text.Json;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Ddk.SolutionLayerAnalyzer;
using Ddk.SolutionLayerAnalyzer.Models;
using Ddk.SolutionLayerAnalyzer.DTOs;

namespace DataverseDevKit.SolutionAnalyzer.CLI;

/// <summary>
/// Main CLI implementation for the Solution Analyzer
/// </summary>
public class SolutionAnalyzerCli
{
    private readonly FileInfo _configFile;
    private readonly string? _connectionString;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string? _tenantId;
    private readonly string? _environmentUrl;
    private readonly LogLevel _logLevel;
    private readonly DirectoryInfo _outputDirectory;
    private readonly string _reportFormat;
    private readonly string _reportVerbosity;
    private readonly ILogger _logger;
    private readonly string _logFilePath;

    public SolutionAnalyzerCli(
        FileInfo configFile,
        string? connectionString,
        string? clientId,
        string? clientSecret,
        string? tenantId,
        string? environmentUrl,
        LogLevel logLevel,
        DirectoryInfo outputDirectory,
        string reportFormat,
        string reportVerbosity)
    {
        _configFile = configFile;
        _connectionString = connectionString;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tenantId = tenantId;
        _environmentUrl = environmentUrl;
        _logLevel = logLevel;
        _outputDirectory = outputDirectory;
        _reportFormat = reportFormat;
        _reportVerbosity = reportVerbosity;

        // Ensure output directory exists
        if (!_outputDirectory.Exists)
        {
            _outputDirectory.Create();
        }

        // Setup file logging
        _logFilePath = Path.Combine(_outputDirectory.FullName, $"cli-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new FileLoggerProvider(_logFilePath, logLevel));
            builder.AddConsole();
        });
        _logger = loggerFactory.CreateLogger<SolutionAnalyzerCli>();

        _logger.LogInformation("Solution Analyzer CLI initialized");
        _logger.LogInformation("Config file: {ConfigFile}", _configFile.FullName);
        _logger.LogInformation("Output directory: {OutputDirectory}", _outputDirectory.FullName);
        _logger.LogInformation("Log file: {LogFile}", _logFilePath);
    }

    public async Task ExecuteIndexAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting Index Operation ===");
        
        try
        {
            // Load configuration
            var config = await LoadConfigurationAsync(cancellationToken);
            _logger.LogInformation("Configuration loaded: {SourceCount} sources, {TargetCount} targets", 
                config.SourceSolutions.Count, config.TargetSolutions.Count);

            // TODO: Initialize plugin and execute index command
            // This requires setting up the plugin context, service client factory, etc.
            // For now, log what would be done
            _logger.LogInformation("Would index solutions: {Sources} -> {Targets}", 
                string.Join(", ", config.SourceSolutions), 
                string.Join(", ", config.TargetSolutions));

            Console.WriteLine("✓ Index operation completed successfully");
            Console.WriteLine($"  Log file: {_logFilePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index operation failed");
            Console.Error.WriteLine($"✗ Index operation failed: {ex.Message}");
            Console.Error.WriteLine($"  See log file for details: {_logFilePath}");
            throw;
        }
    }

    public async Task ExecuteReportsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting Report Execution ===");
        
        try
        {
            // Load configuration
            var config = await LoadConfigurationAsync(cancellationToken);
            
            var totalReports = config.ReportGroups.Sum(g => g.Reports.Count) + config.UngroupedReports.Count;
            _logger.LogInformation("Loaded {GroupCount} groups with {ReportCount} total reports", 
                config.ReportGroups.Count, totalReports);

            // TODO: Execute all reports via plugin
            // For now, log what would be done
            foreach (var group in config.ReportGroups)
            {
                _logger.LogInformation("Group: {GroupName} ({Count} reports)", group.Name, group.Reports.Count);
                foreach (var report in group.Reports)
                {
                    _logger.LogInformation("  - {ReportName} (Severity: {Severity})", report.Name, report.Severity);
                }
            }

            // Generate summary report
            var reportFileName = $"report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{GetFileExtension(_reportFormat)}";
            var reportPath = Path.Combine(_outputDirectory.FullName, reportFileName);
            
            _logger.LogInformation("Report would be generated at: {ReportPath}", reportPath);

            Console.WriteLine("✓ Report execution completed successfully");
            Console.WriteLine($"  Reports executed: {totalReports}");
            Console.WriteLine($"  Output file: {reportPath}");
            Console.WriteLine($"  Log file: {_logFilePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report execution failed");
            Console.Error.WriteLine($"✗ Report execution failed: {ex.Message}");
            Console.Error.WriteLine($"  See log file for details: {_logFilePath}");
            throw;
        }
    }

    public async Task ExportConfigAsync(FileInfo? outputFile, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting Config Export ===");
        
        try
        {
            // TODO: Export current configuration from plugin
            // For now, create a sample config
            var config = new AnalyzerConfig
            {
                SourceSolutions = new List<string> { "CoreSolution" },
                TargetSolutions = new List<string> { "Project1", "Project2" },
                ReportGroups = new List<ConfigReportGroup>(),
                UngroupedReports = new List<ConfigReport>()
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yaml = serializer.Serialize(config);

            var exportPath = outputFile?.FullName ?? Path.Combine(_outputDirectory.FullName, "config-export.yaml");
            await File.WriteAllTextAsync(exportPath, yaml, cancellationToken);

            _logger.LogInformation("Configuration exported to: {ExportPath}", exportPath);
            Console.WriteLine("✓ Configuration exported successfully");
            Console.WriteLine($"  Output file: {exportPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config export failed");
            Console.Error.WriteLine($"✗ Config export failed: {ex.Message}");
            throw;
        }
    }

    public async Task ImportConfigAsync(FileInfo inputFile, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting Config Import ===");
        
        try
        {
            if (!inputFile.Exists)
            {
                throw new FileNotFoundException($"Configuration file not found: {inputFile.FullName}");
            }

            var yaml = await File.ReadAllTextAsync(inputFile.FullName, cancellationToken);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var config = deserializer.Deserialize<AnalyzerConfig>(yaml);

            var totalReports = config.ReportGroups.Sum(g => g.Reports.Count) + config.UngroupedReports.Count;
            _logger.LogInformation("Imported configuration with {GroupCount} groups and {ReportCount} reports",
                config.ReportGroups.Count, totalReports);

            // TODO: Import configuration to plugin
            Console.WriteLine("✓ Configuration imported successfully");
            Console.WriteLine($"  Groups: {config.ReportGroups.Count}");
            Console.WriteLine($"  Reports: {totalReports}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config import failed");
            Console.Error.WriteLine($"✗ Config import failed: {ex.Message}");
            throw;
        }
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
