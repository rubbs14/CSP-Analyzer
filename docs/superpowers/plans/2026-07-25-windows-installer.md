# Windows Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a real per-user Windows installer (`CspAnalyzer-Setup-<version>.exe`) as an additional Windows release artifact, alongside the existing zip, that installs silently with a Start Menu entry and an uninstaller, no admin prompt required.

**Architecture:** A new script (`scripts/package/package-installer.ps1`) wraps the already-assembled `package.ps1` output directory (`artifacts/CspAnalyzer-windows-win-x64/`) by generating an Inno Setup `.iss` script and compiling it with `iscc.exe`. CI's `package` job gets new steps, gated to the `windows-x64` leg only, that resolve a version from the latest git tag (same mechanism used for the Linux rpm work), run the script, then verify the result by actually silent-installing it, checking files/shortcut/registry key, silent-uninstalling it, and checking those are gone.

**Tech Stack:** PowerShell 7 (`pwsh`, matching the existing `package.ps1`), Inno Setup 6 (`iscc.exe`, preinstalled on the `windows-latest` GitHub-hosted runner image), GitHub Actions (`workflow_dispatch`).

## Global Constraints

- Installer is **additive** — the existing Windows zip artifact is untouched.
- Windows `x64` only (matches `win-x64` rid). No arm64 installer.
- Per-user install: `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\CspAnalyzer`. No UAC prompt.
- Fixed `AppId={{D6B0EA6C-E5EC-4259-B860-3D199A86A829}` (GUID generated once for this spec) — hardcoded in the `.iss` template so all versions target the same registry entry for upgrade/uninstall.
- `AppName=CSP Analyzer`, `DefaultGroupName=CSP Analyzer`.
- Output: `OutputBaseFilename=CspAnalyzer-Setup-<Version>`, written to `artifacts/`.
- `SetupIconFile` and the Start Menu shortcut icon both point at `dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico` directly — no `.ico` → `.png` conversion needed (unlike the Linux rpm work).
- New CI steps are gated `if: matrix.os-name == 'windows'`.
- Version resolves the same way as the (separately shipped) Linux rpm work: `git describe --tags --abbrev=0`, leading `v` stripped, `-` replaced with `.`, falls back to `v0.0.0-dev`-style sanitized value if no tag is reachable — never fails the build over a missing tag.
- Any value interpolated from `${{ }}` GitHub Actions expressions into a `run:` script body MUST go through a step-level `env:` block and be referenced as an environment variable (`$env:VAR` in pwsh) — never spliced directly into the script text. (This is a real script-injection anti-pattern fixed during the Linux rpm work's review; this plan avoids it from the start.)
- No GUI smoke test of the installed app — silent install/uninstall only. Real app behavior is already covered by `RealDemoDatasetRunTests.cs`.
- This dev box has no Windows, no `pwsh`, and no Inno Setup installed (checked directly — same constraint documented for the rpm work). The only way to actually exercise this script end-to-end is a real `workflow_dispatch` CI run on `windows-latest` — that is the test gate for both tasks below, not a local run.

---

## File Structure

- **Create:** `scripts/package/package-installer.ps1` — generates an Inno Setup `.iss` script from an already-assembled `package.ps1` output dir and compiles it via `iscc.exe`. Single responsibility: turn an assembled `win-x64` output dir into a `.exe` installer. Does not itself run `dotnet publish`/`pyinstaller`/`package.ps1`.
- **Modify:** `.github/workflows/ci.yml` — `package` job gains `fetch-depth: 0` on checkout (if not already present) and 6 new steps (resolve version, build installer, verify install, verify uninstall, upload artifact), all gated to the windows leg.

---

### Task 1: `scripts/package/package-installer.ps1`

**Files:**
- Create: `scripts/package/package-installer.ps1`

**Interfaces:**
- Consumes: `artifacts/CspAnalyzer-windows-win-x64/` (the directory `package.ps1 -Rid win-x64 -OsName windows` produces — same `CspAnalyzer-$OsName-$Rid` naming convention already established there), and `dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico` (existing asset, confirmed present).
- Produces: CLI contract `./scripts/package/package-installer.ps1 -Version <string>` (mandatory, no leading `v`, no embedded `-` — see Task 2's sanitization, same shape as the rpm work's `-Version`). Writes the result to `artifacts/CspAnalyzer-Setup-<Version>.exe`. Task 2's CI step calls this exact contract.

- [ ] **Step 1: Write `scripts/package/package-installer.ps1`**

```powershell
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
```

- [ ] **Step 2: Commit**

```bash
git add scripts/package/package-installer.ps1
git commit -m "$(cat <<'EOF'
package: add Windows installer build script (Inno Setup, additive to existing zip)

Generates an .iss from package.ps1's assembled win-x64 output (per-user
install, no UAC, fixed AppId for stable upgrades) and compiles it via
iscc.exe. Not yet wired into CI - see next commit.
EOF
)"
```

This script cannot be run on this dev box (no Windows/`pwsh`/Inno Setup installed here — see Global Constraints). It is exercised for real in Task 2's CI run, which is the actual test gate for this task too.

---

### Task 2: Wire into `ci.yml`'s `package` job and verify with a real CI run

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `scripts/package/package-installer.ps1 -Version <string>` (Task 1's exact CLI contract), and the existing `package` job's `matrix.os-name` (`'linux'`/`'windows'`/`'macos'`) / `matrix.rid` variables already used by the "Assemble package" step.
- Produces: a `CspAnalyzer-windows-installer` artifact on `workflow_dispatch` runs (visible in the Actions run's Artifacts list, alongside the existing zip artifacts).

- [ ] **Step 1: Add `fetch-depth: 0` to the `package` job's checkout (if not already present)**

Read `.github/workflows/ci.yml`'s `package:` job checkout step first — the Linux rpm work (a separate, independently-landing branch) may have already added this. If it still reads:

```yaml
      - uses: actions/checkout@v4
```

change it to:

```yaml
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
```

(Needed because `git describe --tags` in Step 2 below requires tag history, and `actions/checkout@v4` defaults to a single-commit shallow clone with no tags. If the rpm branch's change already landed here first, this step is a no-op — do not add a second `fetch-depth` key.)

- [ ] **Step 2: Add the installer build/verify/upload steps**

Still in the `package:` job, immediately after the existing `Assemble package` step and its `- uses: actions/upload-artifact@v4` step (the zip upload — leave that step untouched), add:

```yaml
      - name: Resolve installer version
        if: matrix.os-name == 'windows'
        shell: pwsh
        run: |
          $rawVersion = git describe --tags --abbrev=0 2>$null
          if (-not $rawVersion) { $rawVersion = "v0.0.0-dev" }
          $installerVersion = $rawVersion -replace '^v', '' -replace '-', '.'
          "INSTALLER_VERSION=$installerVersion" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8

      - name: Build Windows installer
        if: matrix.os-name == 'windows'
        shell: pwsh
        env:
          INSTALLER_VERSION: ${{ env.INSTALLER_VERSION }}
        run: ./scripts/package/package-installer.ps1 -Version $env:INSTALLER_VERSION

      - name: Verify installer installs cleanly
        if: matrix.os-name == 'windows'
        shell: pwsh
        env:
          INSTALLER_VERSION: ${{ env.INSTALLER_VERSION }}
        run: |
          $installerPath = "artifacts\CspAnalyzer-Setup-$env:INSTALLER_VERSION.exe"
          Start-Process -FilePath $installerPath -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -Wait

          $installDir = Join-Path $env:LOCALAPPDATA "CspAnalyzer"
          if (-not (Test-Path (Join-Path $installDir "CspAnalyzer.Desktop.exe"))) {
              throw "app exe missing after install"
          }
          if (-not (Test-Path (Join-Path $installDir "csp-backend\csp-backend.exe"))) {
              throw "backend exe missing after install"
          }
          $shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CSP Analyzer\CSP Analyzer.lnk"
          if (-not (Test-Path $shortcut)) {
              throw "Start Menu shortcut missing after install"
          }
          $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D6B0EA6C-E5EC-4259-B860-3D199A86A829}_is1"
          if (-not (Test-Path $uninstallKey)) {
              throw "uninstall registry key missing after install"
          }

      - name: Verify installer uninstalls cleanly
        if: matrix.os-name == 'windows'
        shell: pwsh
        run: |
          $installDir = Join-Path $env:LOCALAPPDATA "CspAnalyzer"
          $uninstaller = Join-Path $installDir "unins000.exe"
          Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES" -Wait

          # Inno's uninstaller copies itself to %TEMP% and relaunches so it
          # can delete its own running exe - the original process (the one
          # -Wait blocks on) can exit before that copy finishes deleting
          # $installDir. Poll instead of trusting -Wait alone.
          $deadline = (Get-Date).AddSeconds(30)
          while ((Test-Path $installDir) -and (Get-Date) -lt $deadline) {
              Start-Sleep -Seconds 1
          }
          if (Test-Path $installDir) {
              throw "install dir still present after uninstall (timed out waiting)"
          }
          $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D6B0EA6C-E5EC-4259-B860-3D199A86A829}_is1"
          if (Test-Path $uninstallKey) {
              throw "uninstall registry key still present after uninstall"
          }

      - name: Upload installer artifact
        if: matrix.os-name == 'windows'
        uses: actions/upload-artifact@v4
        with:
          name: CspAnalyzer-windows-installer
          path: artifacts/CspAnalyzer-Setup-*.exe
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
ci: build and verify a Windows installer on the windows-x64 package leg

Additive to the existing zip: resolves a version from the latest git
tag, runs package-installer.ps1, silent-installs and checks app exe,
backend exe, Start Menu shortcut, and uninstall registry key, then
silent-uninstalls and checks they're gone. Uploads as
CspAnalyzer-windows-installer.
EOF
)"
```

- [ ] **Step 4: Push and manually trigger the `package` workflow**

```bash
git push origin master
```

Then in the GitHub UI: Actions tab → "CI" workflow → "Run workflow" (`workflow_dispatch`) on `master`.

- [ ] **Step 5: Inspect the run — this is the real test gate for Task 1 and Task 2 together**

On the `windows-latest` / win-x64 leg of the `package` job, confirm:
- "Build Windows installer" step succeeds (no thrown error from `package-installer.ps1`).
- "Verify installer installs cleanly" step exits 0 (all four assertions pass: app exe, backend exe, Start Menu shortcut, uninstall registry key).
- "Verify installer uninstalls cleanly" step exits 0 (install dir and registry key both gone).
- The run's Artifacts list shows `CspAnalyzer-windows-installer` alongside the existing zip artifacts (and the Linux rpm artifact, if that branch has landed by then).

If any of these fail, fix `package-installer.ps1` or the `ci.yml` steps and push a new commit — do not merge/consider this done until a real `workflow_dispatch` run shows all green.

---

## Self-Review

**Spec coverage:**
- New script wrapping `package.ps1` output, generating `.iss`, compiling via `iscc` → Task 1.
- `AppId`, `AppName`, `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\CspAnalyzer`, `DefaultGroupName`, `OutputDir`/`OutputBaseFilename`, `SetupIconFile`, `[Files]`/`[Icons]` sections → Task 1, Step 1 — matches spec's Decision section verbatim.
- CI gating to `matrix.os-name == 'windows'`, `fetch-depth: 0`, version resolution (git tag, `v` stripped, fallback), silent install verify (4 assertions), silent uninstall verify, separate artifact upload → Task 2.
- Script-injection-safe `env:` pattern for the CI version value → Task 2, Step 2 (`Build Windows installer` step) — matches the fix established during the rpm work's review, applied proactively here.
- Out-of-scope items (code signing, MSI/WiX, per-machine install, auto-update, arm64, GUI smoke test, replacing the zip, auto-attaching to Releases) — none of them appear in either task; nothing to trim.

**Placeholder scan:** no TBD/TODO; every step has literal file content or literal commands; no "similar to Task N" back-references — Task 2 restates the full YAML block rather than pointing at Task 1's prose.

**Type/name consistency:** `-Version` param name and the `artifacts/CspAnalyzer-Setup-<Version>.exe` output path match between Task 1's script and Task 2's `Build Windows installer` step (`env.INSTALLER_VERSION`) and `Verify installer installs cleanly`/`Upload installer artifact` steps (`artifacts/CspAnalyzer-Setup-*.exe` glob covers the exact filename Task 1 produces). The fixed AppId GUID (`D6B0EA6C-E5EC-4259-B860-3D199A86A829`) matches exactly between Task 1's `.iss` template and Task 2's registry-key assertions in both the install and uninstall verify steps. Directory name `CspAnalyzer-windows-win-x64` matches the existing `package.ps1`'s `$OsName-$Rid` convention (`os-name: windows`, `rid: win-x64` from `ci.yml`'s matrix, unchanged by this plan) — verified by re-reading `package.ps1` and the current `ci.yml` matrix before writing Task 1.
