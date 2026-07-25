<#
.SYNOPSIS
  Wraps the already-assembled Linux package.ps1 output
  (artifacts/CspAnalyzer-linux-linux-x64) into an rpm via fpm:
  /opt/csp-analyzer install dir, /usr/bin wrapper, .desktop entry, icon.
  Run from the repo root, after `package.ps1 -Rid linux-x64 -OsName linux`
  has already produced its output (the CI package job's linux-x64 leg runs
  both - see .github/workflows/ci.yml's `package` job). Linux-only: fpm,
  rpmbuild, and convert are Linux tools, so this script is only ever
  invoked on the ubuntu-latest runner leg, never windows/macos.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version   # e.g. 2.0.1 or 0.0.0.dev - no leading "v", no "-"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$assembledDir = Join-Path $repoRoot "artifacts/CspAnalyzer-linux-linux-x64"
$iconIco = Join-Path $repoRoot "dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsDir "rpm-staging"
$rpmPath = Join-Path $artifactsDir "csp-analyzer-$Version.x86_64.rpm"

if (-not (Test-Path $assembledDir)) {
    throw "Assembled package not found at $assembledDir - run 'package.ps1 -Rid linux-x64 -OsName linux' first."
}
if (-not (Test-Path $iconIco)) {
    throw "Icon not found at $iconIco."
}
foreach ($tool in @("fpm", "convert", "rpmbuild")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool not found on PATH - required to build the rpm."
    }
}

if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}

$optDir = Join-Path $stagingDir "opt/csp-analyzer"
$binDir = Join-Path $stagingDir "usr/bin"
$appsDir = Join-Path $stagingDir "usr/share/applications"
$iconDir = Join-Path $stagingDir "usr/share/icons/hicolor/256x256/apps"
New-Item -ItemType Directory -Force -Path $optDir, $binDir, $appsDir, $iconDir | Out-Null

Copy-Item -Path "$assembledDir/*" -Destination $optDir -Recurse -Force

# Defensively re-assert executable bits on the binaries, since Copy-Item on
# PowerShell Core may not reliably preserve Unix permission bits (precedent in package.ps1).
$executablesToMarkRunnable = @(
    (Join-Path $optDir "CspAnalyzer.Desktop"),
    (Join-Path $optDir "csp-backend" "csp-backend")
)
foreach ($executablePath in $executablesToMarkRunnable) {
    if (Test-Path $executablePath) {
        chmod +x $executablePath
    }
}

$wrapperPath = Join-Path $binDir "csp-analyzer"
Set-Content -Path $wrapperPath -Value @'
#!/bin/sh
exec /opt/csp-analyzer/CspAnalyzer.Desktop "$@"
'@
chmod +x $wrapperPath

$desktopPath = Join-Path $appsDir "csp-analyzer.desktop"
Set-Content -Path $desktopPath -Value @'
[Desktop Entry]
Type=Application
Name=CSP Analyzer
Exec=csp-analyzer
Icon=csp-analyzer
Categories=Science;Chemistry;
Terminal=false
'@

$iconPngPath = Join-Path $iconDir "csp-analyzer.png"
# Frames are stored smallest-first in this .ico; "-delete 0--2" drops all
# but the last (largest) frame, then "!" forces an exact 256x256 output
# regardless of the source frame's exact aspect ratio. "${iconIco}" (curly
# braces) rather than "$iconIco" avoids PowerShell parsing "[...]" as
# string-indexing inside the double-quoted string that follows.
& convert "${iconIco}" -delete 0--2 -resize "256x256!" $iconPngPath
if ($LASTEXITCODE -ne 0) {
    throw "convert exited with code $LASTEXITCODE"
}

if (Test-Path $rpmPath) {
    Remove-Item $rpmPath
}

& fpm -s dir -t rpm `
    -n csp-analyzer -v $Version -a x86_64 `
    --license MIT `
    --description "CSP-Analyzer: NMR chemical shift perturbation analysis" `
    --url "https://github.com/rubbs14/CSP-Analyzer" `
    -p $rpmPath `
    -C $stagingDir opt usr
if ($LASTEXITCODE -ne 0) {
    throw "fpm exited with code $LASTEXITCODE"
}

Write-Host "Packaged: $rpmPath"
