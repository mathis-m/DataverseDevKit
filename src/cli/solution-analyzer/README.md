# Dataverse DevKit Solution Analyzer CLI

Enterprise-ready command-line tool for analyzing Dataverse solution component layering and generating detailed reports.

## Features

- **YAML Configuration**: Define analysis settings and reports in human-readable YAML files
- **Multiple Output Formats**: Generate reports in YAML, JSON, or CSV for Excel processing
- **Report Verbosity Levels**: Control detail level (basic, medium, verbose)
- **Flexible Authentication**: Support for interactive login, connection strings, and service principals
- **Enterprise Reporting**: Define severity levels, actions, and group reports for organizational needs
- **File Logging**: Detailed logs for troubleshooting and audit trails

## Installation

Build the CLI tool:

```bash
cd src/cli/solution-analyzer/DataverseDevKit.SolutionAnalyzer.CLI
dotnet build -c Release
```

The executable will be available at `bin/Release/net10.0/ddk-solution-analyzer`

## Quick Start

1. **Create a configuration file** (see `example-config.yaml`):

```yaml
sourceSolutions:
  - CoreSolution
targetSolutions:
  - Project1
  - Project2
reportGroups:
  - name: Project 1 Analysis
    reports:
      - name: Empty Layers
        severity: Information
        # ... query definition
```

2. **Index your solutions**:

```bash
ddk-solution-analyzer index \
  --config analyzer-config.yaml \
  --environment-url https://yourorg.crm.dynamics.com \
  --output ./reports
```

3. **Run all reports**:

```bash
ddk-solution-analyzer run \
  --config analyzer-config.yaml \
  --environment-url https://yourorg.crm.dynamics.com \
  --format yaml \
  --report-verbosity medium \
  --output ./reports
```

## Commands

### `index`

Build an index of solutions, components, and their layers.

```bash
ddk-solution-analyzer index \
  --config <config-file> \
  --environment-url <url> \
  [--verbosity <level>] \
  [--output <directory>]
```

### `run`

Execute all reports defined in the configuration file.

```bash
ddk-solution-analyzer run \
  --config <config-file> \
  --environment-url <url> \
  [--format yaml|json|csv] \
  [--report-verbosity basic|medium|verbose] \
  [--verbosity <level>] \
  [--output <directory>]
```

### `export`

Export current report configuration to YAML.

```bash
ddk-solution-analyzer export \
  --config <config-file> \
  --environment-url <url> \
  [--file <output-file>] \
  [--output <directory>]
```

### `import`

Import report configuration from YAML.

```bash
ddk-solution-analyzer import \
  --config <config-file> \
  --environment-url <url> \
  --file <import-file> \
  [--output <directory>]
```

## Global Options

### Authentication Options

**Connection String** (easiest for testing):
```bash
--connection-string "AuthType=OAuth;Username=user@domain.com;..."
```

**Service Principal** (recommended for automation):
```bash
--client-id <app-id> \
--client-secret <secret> \
--tenant-id <tenant-id> \
--environment-url <url>
```

**Interactive** (default):
```bash
--environment-url <url>
```

### Output Options

- `--output, -o`: Output directory for reports and logs (default: current directory)
- `--format, -f`: Report format - yaml, json, or csv (default: yaml)
- `--report-verbosity, -rv`: Report detail level - basic, medium, or verbose (default: basic)

### Logging Options

- `--verbosity, -v`: Log verbosity - quiet, minimal, normal, detailed, or diagnostic (default: normal)

## Report Verbosity Levels

### Basic
- Component ID, type, logical name, display name
- List of solutions containing the component
- Minimal detail for quick overview

### Medium
- All basic information
- List of changed attributes per layer
- Good balance for most scenarios

### Verbose
- All medium information
- Full attribute change details (old/new values)
- Maximum detail for deep analysis

## Output Formats

### YAML
Human-readable, structured format ideal for reviewing reports manually.

### JSON
Machine-readable format perfect for integration with other tools and CI/CD pipelines.

### CSV
Flat format optimized for Excel processing and pivot tables.

## Configuration File Structure

```yaml
# Global settings
sourceSolutions:
  - CoreSolution
targetSolutions:
  - Project1
  - Project2

# Optional: Limit to specific component types
componentTypes:
  - 1   # Entity
  - 24  # SystemForm
  - 26  # SavedQuery

# Organized reports
reportGroups:
  - name: Group Name
    displayOrder: 1
    reports:
      - name: Report Name
        description: What this report checks
        severity: Information|Warning|Critical
        recommendedAction: What to do with findings
        displayOrder: 1
        queryJson: '{ ... filter query ... }'

# Reports not in any group
ungroupedReports:
  - name: Standalone Report
    # ... same fields as above
```

## Examples

### CI/CD Integration

```bash
#!/bin/bash
# Run solution analysis in CI/CD pipeline

ddk-solution-analyzer run \
  --config solution-analysis.yaml \
  --client-id $AZURE_CLIENT_ID \
  --client-secret $AZURE_CLIENT_SECRET \
  --tenant-id $AZURE_TENANT_ID \
  --environment-url $DATAVERSE_URL \
  --format csv \
  --report-verbosity medium \
  --verbosity minimal \
  --output ./build/reports

# Check exit code
if [ $? -ne 0 ]; then
  echo "Analysis failed!"
  exit 1
fi

echo "Analysis completed successfully"
```

### Generate Multiple Format Reports

```bash
# YAML for manual review
ddk-solution-analyzer run --config config.yaml -f yaml -o ./reports/yaml

# JSON for automation
ddk-solution-analyzer run --config config.yaml -f json -o ./reports/json

# CSV for Excel
ddk-solution-analyzer run --config config.yaml -f csv -o ./reports/csv
```

## Troubleshooting

- **Log files**: Check the CLI log file in the output directory for detailed execution logs
- **Verbosity**: Increase verbosity with `--verbosity diagnostic` for maximum detail
- **Authentication**: Verify credentials and permissions if connection fails

## See Also

- [Example Configuration](example-config.yaml)
- [Plugin Documentation](../../plugins/solution-layer-analyzer/README.md)
- [Query Syntax Reference](../../plugins/solution-layer-analyzer/docs/query-syntax.md)
