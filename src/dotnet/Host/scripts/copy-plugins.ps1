# copy-plugins.ps1
# Copies plugin dist folders to the Host wwwroot/plugins directory

param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,
    
    [Parameter(Mandatory=$true)]
    [string]$TargetPath
)

Write-Host "Copying plugins from: $SourcePath"
Write-Host "Copying plugins to: $TargetPath"

if (-not (Test-Path $SourcePath)) {
    Write-Host "Source path does not exist: $SourcePath"
    exit 0
}

# Ensure target directory exists
if (-not (Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

# Find and copy each plugin's dist folder
Get-ChildItem -Path $SourcePath -Directory | ForEach-Object {
    $pluginFolder = $_.FullName
    $distPath = Join-Path $pluginFolder 'dist'
    $manifestPath = Join-Path $distPath 'plugin.manifest.json'
    
    if (Test-Path $distPath) {
        # Read plugin ID from manifest
        if (Test-Path $manifestPath) {
            $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
            $pluginId = $manifest.id
        } else {
            Write-Host "Warning: No manifest found in dist for $($_.Name), skipping"
            return
        }
        
        if (-not $pluginId) {
            Write-Host "Warning: No plugin ID in manifest for $($_.Name), skipping"
            return
        }
        
        $targetPluginPath = Join-Path $TargetPath $pluginId
        
        # Remove existing plugin folder
        if (Test-Path $targetPluginPath) {
            Remove-Item -Path $targetPluginPath -Recurse -Force
        }
        
        # Create target directory and copy dist contents preserving structure
        New-Item -ItemType Directory -Path $targetPluginPath -Force | Out-Null
        Copy-Item -Path "$distPath\*" -Destination $targetPluginPath -Recurse -Force
        
        Write-Host "Copied plugin: $pluginId"
    }
}

Write-Host "Plugin copy complete"
