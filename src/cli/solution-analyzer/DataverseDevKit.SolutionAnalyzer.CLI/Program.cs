using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using DataverseDevKit.SolutionAnalyzer.CLI;

// Root command
var rootCommand = new RootCommand("Dataverse DevKit Solution Layer Analyzer CLI - Enterprise reporting tool for Dataverse solution analysis");

// Global options
var configOption = new Option<FileInfo>(
    aliases: new[] { "--config", "-c" },
    description: "Path to the YAML configuration file"
);
configOption.IsRequired = true;

var connectionStringOption = new Option<string>(
    aliases: new[] { "--connection-string", "-cs" },
    description: "Dataverse connection string (alternative to interactive auth)"
);

var clientIdOption = new Option<string>(
    aliases: new[] { "--client-id" },
    description: "Azure AD client ID for service principal authentication"
);

var clientSecretOption = new Option<string>(
    aliases: new[] { "--client-secret" },
    description: "Azure AD client secret for service principal authentication"
);

var tenantIdOption = new Option<string>(
    aliases: new[] { "--tenant-id" },
    description: "Azure AD tenant ID for service principal authentication"
);

var environmentUrlOption = new Option<string>(
    aliases: new[] { "--environment-url", "-e" },
    description: "Dataverse environment URL"
);

var verbosityOption = new Option<string>(
    aliases: new[] { "--verbosity", "-v" },
    getDefaultValue: () => "normal",
    description: "Log verbosity level (quiet, minimal, normal, detailed, diagnostic)"
);

var outputPathOption = new Option<DirectoryInfo>(
    aliases: new[] { "--output", "-o" },
    getDefaultValue: () => new DirectoryInfo("."),
    description: "Output directory for reports and logs"
);

var reportFormatOption = new Option<string>(
    aliases: new[] { "--format", "-f" },
    getDefaultValue: () => "yaml",
    description: "Report output format (yaml, json, csv)"
);

var reportVerbosityOption = new Option<string>(
    aliases: new[] { "--report-verbosity", "-rv" },
    getDefaultValue: () => "basic",
    description: "Report detail level (basic, medium, verbose)"
);

// Add global options
rootCommand.AddOption(configOption);
rootCommand.AddOption(connectionStringOption);
rootCommand.AddOption(clientIdOption);
rootCommand.AddOption(clientSecretOption);
rootCommand.AddOption(tenantIdOption);
rootCommand.AddOption(environmentUrlOption);
rootCommand.AddOption(verbosityOption);
rootCommand.AddOption(outputPathOption);
rootCommand.AddOption(reportFormatOption);
rootCommand.AddOption(reportVerbosityOption);

// Index command
var indexCommand = new Command("index", "Build an index of solutions, components, and their layers");
indexCommand.SetHandler(async (context) =>
{
    var cli = CreateCli(context);
    await cli.ExecuteIndexAsync(context.GetCancellationToken());
});

// Run command - execute all reports from config
var runCommand = new Command("run", "Execute all reports defined in the configuration file");
runCommand.SetHandler(async (context) =>
{
    var cli = CreateCli(context);
    await cli.ExecuteReportsAsync(context.GetCancellationToken());
});

// Export command - export configuration
var exportCommand = new Command("export", "Export current report configuration to YAML");
var exportFileOption = new Option<FileInfo>(
    aliases: new[] { "--file", "-f" },
    description: "Output file path for exported configuration"
);
exportCommand.AddOption(exportFileOption);
exportCommand.SetHandler(async (context) =>
{
    var cli = CreateCli(context);
    var exportFile = context.ParseResult.GetValueForOption(exportFileOption);
    await cli.ExportConfigAsync(exportFile, context.GetCancellationToken());
});

// Import command - import configuration
var importCommand = new Command("import", "Import report configuration from YAML");
var importFileOption = new Option<FileInfo>(
    aliases: new[] { "--file", "-f" },
    description: "Input file path for configuration to import"
);
importFileOption.IsRequired = true;
importCommand.AddOption(importFileOption);
importCommand.SetHandler(async (context) =>
{
    var cli = CreateCli(context);
    var importFile = context.ParseResult.GetValueForOption(importFileOption)!;
    await cli.ImportConfigAsync(importFile, context.GetCancellationToken());
});

rootCommand.AddCommand(indexCommand);
rootCommand.AddCommand(runCommand);
rootCommand.AddCommand(exportCommand);
rootCommand.AddCommand(importCommand);

return await rootCommand.InvokeAsync(args);

static SolutionAnalyzerCli CreateCli(InvocationContext context)
{
    var config = context.ParseResult.GetValueForOption(configOption)!;
    var connectionString = context.ParseResult.GetValueForOption(connectionStringOption);
    var clientId = context.ParseResult.GetValueForOption(clientIdOption);
    var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
    var tenantId = context.ParseResult.GetValueForOption(tenantIdOption);
    var environmentUrl = context.ParseResult.GetValueForOption(environmentUrlOption);
    var verbosity = context.ParseResult.GetValueForOption(verbosityOption)!;
    var output = context.ParseResult.GetValueForOption(outputPathOption)!;
    var reportFormat = context.ParseResult.GetValueForOption(reportFormatOption)!;
    var reportVerbosity = context.ParseResult.GetValueForOption(reportVerbosityOption)!;

    var logLevel = verbosity.ToLowerInvariant() switch
    {
        "quiet" => LogLevel.Error,
        "minimal" => LogLevel.Warning,
        "normal" => LogLevel.Information,
        "detailed" => LogLevel.Debug,
        "diagnostic" => LogLevel.Trace,
        _ => LogLevel.Information
    };

    return new SolutionAnalyzerCli(
        config,
        connectionString,
        clientId,
        clientSecret,
        tenantId,
        environmentUrl,
        logLevel,
        output,
        reportFormat,
        reportVerbosity);
}
