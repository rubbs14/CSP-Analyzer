<#
.SYNOPSIS
  Assembles a self-contained CspAnalyzer package: dotnet publish output +
  model_artifacts/ + the PyInstaller-frozen csp-backend dist, then zips it.
  Run from the repo root, after both `dotnet publish` and `pyinstaller`
  have already produced their outputs (the CI packaging job runs both
  first - see .github/workflows/ci.yml's `package` job).
#>
param(
    [Parameter(Mandatory = $true)][string]$Rid,      # e.g. win-x64, linux-x64, osx-x64
    [Parameter(Mandatory = $true)][string]$OsName    # e.g. windows, linux, macos - used only in the output filename
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$publishDir = Join-Path $repoRoot "dotnet/CspAnalyzer.Desktop/bin/Release/net8.0/$Rid/publish"
$modelArtifactsDir = Join-Path $repoRoot "backend/model_artifacts"
$frozenBackendDistDir = Join-Path $repoRoot "backend/dist/csp-backend"
$artifactsDir = Join-Path $repoRoot "artifacts"
$outputDir = Join-Path $artifactsDir "CspAnalyzer-$OsName-$Rid"
$zipPath = Join-Path $artifactsDir "CspAnalyzer-$OsName-$Rid.zip"

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found at $publishDir - run 'dotnet publish -r $Rid --self-contained' first."
}
if (-not (Test-Path $frozenBackendDistDir)) {
    throw "Frozen backend dist not found at $frozenBackendDistDir - run 'pyinstaller backend/csp-backend.spec' first."
}
if (-not (Test-Path $modelArtifactsDir)) {
    throw "model_artifacts/ not found at $modelArtifactsDir."
}

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Copy-Item -Path "$publishDir/*" -Destination $outputDir -Recurse -Force
Copy-Item -Path $modelArtifactsDir -Destination (Join-Path $outputDir "model_artifacts") -Recurse -Force
Copy-Item -Path $frozenBackendDistDir -Destination (Join-Path $outputDir "csp-backend") -Recurse -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}
Compress-Archive -Path "$outputDir/*" -DestinationPath $zipPath

Write-Host "Packaged: $zipPath"
