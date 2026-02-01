using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Logging;
using DataverseDevKit.SolutionAnalyzer.CLI;

// Root command
var rootCommand = new RootCommand("Dataverse DevKit Solution Layer Analyzer CLI - Execute solution integrity reports for CI/CD monitoring");

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
environmentUrlOption.IsRequired = true;

var verbosityOption = new Option<string>(
    aliases: new[] { "--verbosity", "-v" },
    getDefaultValue: () => "normal",
    description: "Console log verbosity (quiet, minimal, normal, detailed)"
);

var outputPathOption = new Option<DirectoryInfo>(
    aliases: new[] { "--output", "-o" },
    getDefaultValue: () => new DirectoryInfo("."),
    description: "Output directory for reports and plugin logs"
);

var formatOption = new Option<string>(
    aliases: new[] { "--format", "-f" },
    getDefaultValue: () => "yaml",
    description: "Report output format (yaml, json, csv)"
);

var failOnSeverityOption = new Option<string>(
    aliases: new[] { "--fail-on-severity" },
    description: "Fail pipeline if findings of this severity or higher (critical, warning, information)"
);

var maxFindingsOption = new Option<int?>(
    aliases: new[] { "--max-findings" },
    description: "Maximum number of findings allowed before failing"
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
rootCommand.AddOption(formatOption);
rootCommand.AddOption(failOnSeverityOption);
rootCommand.AddOption(maxFindingsOption);

// Main execution - run all reports
rootCommand.SetHandler(async (context) =>
{
    var cli = CreateCli(context);
    var exitCode = await cli.ExecuteReportsAsync(context.GetCancellationToken());
    context.ExitCode = exitCode;
});

return await rootCommand.InvokeAsync(args);

static ReportExecutor CreateCli(InvocationContext context)
{
    var config = context.ParseResult.GetValueForOption(configOption)!;
    var connectionString = context.ParseResult.GetValueForOption(connectionStringOption);
    var clientId = context.ParseResult.GetValueForOption(clientIdOption);
    var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
    var tenantId = context.ParseResult.GetValueForOption(tenantIdOption);
    var environmentUrl = context.ParseResult.GetValueForOption(environmentUrlOption)!;
    var verbosity = context.ParseResult.GetValueForOption(verbosityOption)!;
    var output = context.ParseResult.GetValueForOption(outputPathOption)!;
    var format = context.ParseResult.GetValueForOption(formatOption)!;
    var failOnSeverity = context.ParseResult.GetValueForOption(failOnSeverityOption);
    var maxFindings = context.ParseResult.GetValueForOption(maxFindingsOption);

    var logLevel = verbosity.ToLowerInvariant() switch
    {
        "quiet" => LogLevel.Error,
        "minimal" => LogLevel.Warning,
        "normal" => LogLevel.Information,
        "detailed" => LogLevel.Debug,
        _ => LogLevel.Information
    };

    return new ReportExecutor(
        config,
        environmentUrl,
        connectionString,
        clientId,
        clientSecret,
        tenantId,
        logLevel,
        output,
        format,
        failOnSeverity,
        maxFindings);
}
