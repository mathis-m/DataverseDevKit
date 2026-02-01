# Dataverse DevKit Solution Analyzer CLI

CI/CD monitoring tool for continuously checking Dataverse solution layer integrity.

## Purpose

Execute configured reports against Dataverse environments and return exit codes for pipeline control.

**Design**: Simple executor - just runs reports, nothing more. Plugin logs go to file, CLI logs go to console.

## Quick Start

```bash
ddk-solution-analyzer \
  --config analyzer-config.yaml \
  --environment-url https://yourorg.crm.dynamics.com \
  --fail-on-severity critical
```

## Exit Codes

- **0** - Success (no critical issues or within thresholds)
- **1** - Failure (severity/count thresholds exceeded)
- **2** - Error (execution failed)

## Options

| Option | Description |
|--------|-------------|
| `--config, -c` | YAML configuration file **(required)** |
| `--environment-url, -e` | Dataverse environment URL **(required)** |
| `--client-id` | Azure AD client ID (service principal) |
| `--client-secret` | Azure AD client secret |
| `--tenant-id` | Azure AD tenant ID |
| `--output, -o` | Output directory (default: current) |
| `--format, -f` | Report format: yaml, json, csv (default: yaml) |
| `--verbosity, -v` | Console log level: quiet, minimal, normal, detailed |
| `--fail-on-severity` | Fail on: critical, warning, information |
| `--max-findings` | Max total findings before failing |

## CI/CD Examples

### GitHub Actions

```yaml
- name: Check Solution Integrity
  run: |
    ddk-solution-analyzer \
      --config analyzer-config.yaml \
      --environment-url ${{ secrets.DATAVERSE_URL }} \
      --client-id ${{ secrets.CLIENT_ID }} \
      --client-secret ${{ secrets.CLIENT_SECRET }} \
      --tenant-id ${{ secrets.TENANT_ID }} \
      --fail-on-severity critical \
      --verbosity minimal
```

### Azure DevOps

```yaml
- script: |
    ddk-solution-analyzer \
      --config analyzer-config.yaml \
      --environment-url $(DataverseUrl) \
      --client-id $(ClientId) \
      --client-secret $(ClientSecret) \
      --tenant-id $(TenantId) \
      --fail-on-severity warning
  displayName: 'Solution Integrity Check'
```

### GitLab CI

```yaml
integrity-check:
  script:
    - ddk-solution-analyzer --config config.yaml -e $DATAVERSE_URL 
        --client-id $CLIENT_ID --client-secret $CLIENT_SECRET 
        --fail-on-severity critical --verbosity minimal
```

## Output

```
═══════════════════════════════════════════════════════════
  SOLUTION INTEGRITY REPORT SUMMARY
═══════════════════════════════════════════════════════════
  Reports Executed:    4
  Critical Findings:   2
  Warning Findings:    5
  Info Findings:       3
  Total Findings:      10
───────────────────────────────────────────────────────────
  Output:              report-20260201-120000.yaml
  Plugin Logs:         plugin-20260201-120000.log
═══════════════════════════════════════════════════════════
  Status: ✗ FAILED - Exit code 1
  Reason: Findings with severity 'critical' or higher detected
═══════════════════════════════════════════════════════════
```

## Logging

- **CLI logs** → Console (based on `--verbosity`)
- **Plugin logs** → File (sandboxed, always written to `plugin-{timestamp}.log`)

## Configuration

See `example-config.yaml`:

```yaml
sourceSolutions: [CoreSolution]
targetSolutions: [Project1, Project2]
reportGroups:
  - name: Critical Checks
    reports:
      - name: Conflicting Layers
        severity: Critical
        recommendedAction: Resolve immediately
        queryJson: '...'
```

## Best Practices

| Environment | Recommended Settings |
|-------------|---------------------|
| Development | `--fail-on-severity warning` |
| QA/UAT | `--fail-on-severity critical` |
| Production Gate | `--fail-on-severity critical --max-findings 0` |

