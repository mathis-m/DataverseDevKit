# Build script to compile web frontend and copy to MAUI wwwroot

$webRoot = Join-Path $PSScriptRoot "web"
$shellDist = Join-Path $webRoot "apps\shell\dist"
$pluginDist = Join-Path $webRoot "plugins\first-party\hello-world-ui\dist"
$hostWwwroot = Join-Path $PSScriptRoot "src\dotnet\Host\wwwroot"

# Build web projects
Write-Host "Building web frontend..."
Set-Location $webRoot

if (-not (Test-Path "node_modules")) {
    Write-Host "Installing dependencies..."
    pnpm install
}

Write-Host "Building Shell app..."
pnpm build:shell

Write-Host "Building plugins..."
pnpm build:plugins

# Copy to wwwroot
Write-Host "Copying to MAUI wwwroot..."
if (Test-Path $hostWwwroot) {
    Remove-Item $hostWwwroot\* -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $hostWwwroot | Out-Null

if (Test-Path $shellDist) {
    Copy-Item "$shellDist\*" $hostWwwroot -Recurse -Force
}

if (Test-Path $pluginDist) {
    $pluginWwwroot = Join-Path $hostWwwroot "plugins\hello-world"
    New-Item -ItemType Directory -Force -Path $pluginWwwroot | Out-Null
    Copy-Item "$pluginDist\*" $pluginWwwroot -Recurse -Force
}

Write-Host "Web build complete!" -ForegroundColor Green
