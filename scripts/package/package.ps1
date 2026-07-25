<#
.SYNOPSIS
  Assembles a self-contained CspAnalyzer package: dotnet publish output +
  model_artifacts/ + the PyInstaller-frozen csp-backend dist + the demo
  dataset, then zips it. Run from the repo root, after both `dotnet publish`
  and `pyinstaller` have already produced their outputs (the CI packaging
  job runs both first - see .github/workflows/ci.yml's `package` job).
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
$demoDatasetDir = Join-Path $repoRoot "CSPv2/Demo-dataset"
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
if (-not (Test-Path $demoDatasetDir)) {
    throw "Demo dataset not found at $demoDatasetDir."
}

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Copy-Item -Path "$publishDir/*" -Destination $outputDir -Recurse -Force
Copy-Item -Path $modelArtifactsDir -Destination (Join-Path $outputDir "model_artifacts") -Recurse -Force
Copy-Item -Path $frozenBackendDistDir -Destination (Join-Path $outputDir "csp-backend") -Recurse -Force
Copy-Item -Path $demoDatasetDir -Destination (Join-Path $outputDir "Demo-dataset") -Recurse -Force

if (-not $IsWindows) {
    $executablesToMarkRunnable = @(
        (Join-Path $outputDir "CspAnalyzer.Desktop"),
        (Join-Path $outputDir "csp-backend" "csp-backend")
    )
    foreach ($executablePath in $executablesToMarkRunnable) {
        if (Test-Path $executablePath) {
            chmod +x $executablePath
        }
    }
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

if ($IsWindows) {
    Compress-Archive -Path "$outputDir/*" -DestinationPath $zipPath
} else {
    # Compress-Archive (System.IO.Compression.ZipFile under the hood) does
    # not preserve Unix executable permission bits in the zip's own
    # metadata, regardless of the source files' actual permissions -
    # confirmed by a real CI run producing a non-executable app after
    # download+unzip even after the files were chmod +x'd on disk first.
    # The native `zip` CLI (preinstalled on GitHub's ubuntu-latest/
    # macos-latest runner images) does preserve them correctly.
    Push-Location $outputDir
    try {
        & zip -r -q $zipPath .
        if ($LASTEXITCODE -ne 0) {
            throw "zip exited with code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
}

Write-Host "Packaged: $zipPath"
