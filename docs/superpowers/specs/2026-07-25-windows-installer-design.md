# Windows installer for the release

## Problem

CSP-Analyzer's Windows release artifact (S14, `scripts/package/package.ps1`) is a plain zip: `dotnet publish` output + `model_artifacts/` + PyInstaller-frozen `csp-backend.exe` dist + `Demo-dataset/`. Windows users have no native install path — no Start Menu entry, no `Programs & Features`/uninstaller, no installer double-click experience. This mirrors the gap the Linux rpm work (`docs/superpowers/specs/2026-07-25-rpm-packaging-design.md`) closed for rpm-based distros; this spec closes the equivalent gap for Windows.

## Decision

**1. New script: `scripts/package/package-installer.ps1`**

Runs after `package.ps1` has already assembled `artifacts/CspAnalyzer-windows-win-x64/` (reuses that output rather than re-copying `dotnet publish`/PyInstaller/model_artifacts/demo-dataset itself, exactly as `package-rpm.ps1` does for Linux). Takes `-Version` (required) and:

- Generates an Inno Setup script (`artifacts/installer-staging/CspAnalyzer.iss`) from a template, with:
  - `AppName=CSP Analyzer`, `AppVersion=<Version>`, `AppId={{D6B0EA6C-E5EC-4259-B860-3D199A86A829}` — a fixed GUID, generated once for this spec and hardcoded in the template so upgrades/uninstalls across versions all target the same registry entry (Inno Setup wraps `AppId` values in `{{...}` when they're GUIDs, to escape Inno's own `{constant}` syntax)
  - `PrivilegesRequired=lowest` — per-user install, no UAC/admin prompt
  - `DefaultDirName={localappdata}\CspAnalyzer` (per-user install location)
  - `DefaultGroupName=CSP Analyzer` (Start Menu folder)
  - `OutputDir=artifacts`, `OutputBaseFilename=CspAnalyzer-Setup-<Version>`
  - `SetupIconFile` (compile-time only) points at the existing `dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico` directly, no format conversion needed (unlike the rpm work, which had to convert `.ico` → `.png` for freedesktop icon theming). The `[Icons]` Start Menu shortcut has no `IconFilename` and instead relies on the icon already embedded in `CspAnalyzer.Desktop.exe` via the `.csproj`'s `<ApplicationIcon>` — `IconFilename` on an `[Icons]` entry resolves on the end user's machine, where a build-machine `.ico` path wouldn't exist.
  - `[Files]` section: bundles the entire `artifacts/CspAnalyzer-windows-win-x64/` tree verbatim (recursive, `Flags: recursesubdirs`) into `{app}`
  - `[Icons]` section: one Start Menu shortcut, `{group}\CSP Analyzer.lnk` → `{app}\CspAnalyzer.Desktop.exe`
  - Uninstaller is automatic (Inno Setup always generates `unins000.exe` + a `HKCU\...\Uninstall\{AppId}_is1` registry entry when `PrivilegesRequired=lowest`)
- Invokes `iscc.exe artifacts/installer-staging/CspAnalyzer.iss` (Inno Setup's command-line compiler, preinstalled on the `windows-latest` GitHub-hosted runner image)
- Fails loudly (`$ErrorActionPreference = "Stop"`, matching `package.ps1`/`package-rpm.ps1`'s style) if `iscc.exe`, `package.ps1`'s output dir, or the icon aren't present — no silent fallback.

**2. CI wiring (`ci.yml`, `package` job)**

New steps added after "Assemble package", gated `if: matrix.os-name == 'windows'` (mirroring the rpm work's `matrix.os-name == 'linux'` gating):

- Resolve version: same mechanism already added for the rpm work (`git describe --tags --abbrev=0`, strip leading `v`, `-` → `.`, fallback `0.0.0.dev`) — the `package` job's checkout already has `fetch-depth: 0` from that earlier change, so no further checkout modification is needed. Inno Setup's `AppVersion` field has no dash restriction, but reusing the exact same sanitized `$env:RPM_VERSION`-style value keeps both installers' version strings identical for a given release, which is worth more than the extra permissiveness Inno Setup would technically allow.
- Run `./scripts/package/package-installer.ps1 -Version $env:RPM_VERSION` (same `env:` block + `$env:` pattern used for the rpm build step, for the same script-injection-avoidance reason established during that work's review).
- **Verify the installer actually installs**, silently, on the same `windows-latest` runner (no cross-platform container trick needed here, unlike the rpm work's `rockylinux:9` — the runner already is the target OS):
  ```powershell
  Start-Process -FilePath "artifacts\CspAnalyzer-Setup-$env:RPM_VERSION.exe" -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -Wait
  $installDir = Join-Path $env:LOCALAPPDATA "CspAnalyzer"
  if (-not (Test-Path (Join-Path $installDir "CspAnalyzer.Desktop.exe"))) { throw "app exe missing after install" }
  if (-not (Test-Path (Join-Path $installDir "csp-backend\csp-backend.exe"))) { throw "backend exe missing after install" }
  $shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CSP Analyzer\CSP Analyzer.lnk"
  if (-not (Test-Path $shortcut)) { throw "Start Menu shortcut missing after install" }
  $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D6B0EA6C-E5EC-4259-B860-3D199A86A829}_is1"
  if (-not (Test-Path $uninstallKey)) { throw "uninstall registry key missing after install" }
  ```
  (Same GUID as the `.iss` template's `AppId`, above.)
- **Verify clean uninstall**: run the generated `unins000.exe /VERYSILENT /SUPPRESSMSGBOXES` from the install dir, then assert `$installDir` no longer exists and the uninstall registry key is gone.
- Upload as a separate artifact (`actions/upload-artifact@v4`, `name: CspAnalyzer-windows-installer`, `path: artifacts/CspAnalyzer-Setup-*.exe`) — additive to the existing zip upload, not a replacement.

**3. Package identity**

- Product name: `CSP Analyzer`. Publisher: not set to a real signed identity (see Explicitly out of scope — no code signing). Per-user install only, `win-x64` only (matches the existing `win-x64` rid — no arm64, consistent with S14 and the rpm work never building arm64 either).

## Explicitly out of scope

- Code signing / Authenticode. The generated `setup.exe` and `unins000.exe` are unsigned; Windows SmartScreen will show an "unrecognized publisher" warning on first run, same tradeoff already accepted for the unsigned rpm. A future spec can add signing if a certificate becomes available.
- MSI / WiX Toolset. Inno Setup produces a `setup.exe`, not a Windows Installer `.msi` — no enterprise GPO/SCCM deployment story. Out of scope per the earlier tool-choice decision.
- Per-machine install (`PrivilegesRequired=admin`, `Program Files`, all-users Start Menu). Explicitly chosen against in favor of per-user, no-UAC install.
- Auto-update / update-checking inside the installed app.
- arm64/`win-arm64` installer (S14 never built that rid; not adding it here).
- GUI smoke-testing the installed app (no display interaction beyond silent install/uninstall — launching and interacting with the actual Avalonia window is out of scope, same reasoning as the rpm work's no-GUI-test decision: the app's actual run behavior is already covered by `RealDemoDatasetRunTests.cs`).
- Replacing or touching the existing Windows zip artifact — the installer is additive.
- Auto-attaching build artifacts to GitHub Releases — this remains a manually-triggered `workflow_dispatch` artifact, same as the zip and the rpm.

## Verification steps

1. `scripts/package/package-installer.ps1` runs cleanly against a locally-produced `package.ps1` output dir (requires `iscc.exe` installed locally, or run inside CI — this dev environment has neither Windows nor Inno Setup available, so local execution is not possible here; verified for real only via a `workflow_dispatch` CI run, same constraint documented for the rpm work).
2. The CI `windows-latest` silent-install-and-check step (above) passes: the installer runs to completion, all four post-install assertions pass (app exe, backend exe, Start Menu shortcut, uninstall registry key).
3. The CI silent-uninstall step passes: install dir and registry key are both gone afterward.
4. A `workflow_dispatch` CI run produces `CspAnalyzer-windows-installer` as a downloadable artifact alongside the existing zip artifacts and the Linux rpm artifact.
