<#
.SYNOPSIS
  Wraps the already-assembled Windows package.ps1 output
  (artifacts/CspAnalyzer-windows-win-x64) into a per-user Inno Setup
  installer. Run from the repo root, after
  `package.ps1 -Rid win-x64 -OsName windows` has already produced its
  output (the CI package job's windows-x64 leg runs both - see
  .github/workflows/ci.yml's `package` job). Windows-only: iscc.exe is a
  Windows tool, so this script is only ever invoked on the windows-latest
  runner leg, never linux/macos.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version   # e.g. 2.0.1 or 0.0.0.dev - no leading "v", no "-"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$assembledDir = Join-Path $repoRoot "artifacts/CspAnalyzer-windows-win-x64"
$iconIco = Join-Path $repoRoot "dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico"
$artifactsDir = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsDir "installer-staging"
$issPath = Join-Path $stagingDir "CspAnalyzer.iss"
$installerPath = Join-Path $artifactsDir "CspAnalyzer-Setup-$Version.exe"

if (-not (Test-Path $assembledDir)) {
    throw "Assembled package not found at $assembledDir - run 'package.ps1 -Rid win-x64 -OsName windows' first."
}
if (-not (Test-Path $iconIco)) {
    throw "Icon not found at $iconIco."
}
if (-not (Get-Command "iscc" -ErrorAction SilentlyContinue)) {
    throw "iscc (Inno Setup compiler) not found on PATH - required to build the installer."
}

if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

# AppId's "{{...}" is Inno Setup's own escape for a literal "{" - it
# renders as "{GUID}" in the compiled installer. Fixed GUID so every
# version's installer/uninstaller targets the same Uninstall registry
# entry (upgrades replace in place instead of leaving two entries).
$issContent = @"
[Setup]
AppId={{D6B0EA6C-E5EC-4259-B860-3D199A86A829}
AppName=CSP Analyzer
AppVersion=$Version
DefaultDirName={localappdata}\CspAnalyzer
DefaultGroupName=CSP Analyzer
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=$artifactsDir
OutputBaseFilename=CspAnalyzer-Setup-$Version
SetupIconFile=$iconIco
Compression=lzma
SolidCompression=yes

[Files]
Source: "$assembledDir\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\CSP Analyzer"; Filename: "{app}\CspAnalyzer.Desktop.exe"; IconFilename: "$iconIco"
"@

Set-Content -Path $issPath -Value $issContent

if (Test-Path $installerPath) {
    Remove-Item $installerPath
}

& iscc $issPath
if ($LASTEXITCODE -ne 0) {
    throw "iscc exited with code $LASTEXITCODE"
}

Write-Host "Packaged: $installerPath"
