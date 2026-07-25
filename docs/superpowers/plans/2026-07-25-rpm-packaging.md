# RPM Packaging for Linux Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a real rpm (`csp-analyzer-<version>.x86_64.rpm`) as an additional Linux release artifact, alongside the existing zip, that installs cleanly with `rpm -i` and shows up in the desktop app menu.

**Architecture:** A new script (`scripts/package/package-rpm.ps1`) wraps the already-assembled `package.ps1` output directory into an FHS-style staging tree (`/opt/csp-analyzer`, `/usr/bin/csp-analyzer` wrapper, `.desktop` entry, icon), then shells out to `fpm` to produce the `.rpm`. CI's `package` job gets new steps, gated to the `linux-x64` leg only, that install the build tools, resolve a version from the latest git tag, run the script, and verify the result both statically (`rpm -qlp`/`rpm -qip`) and by actually installing it inside a `rockylinux:9` container.

**Tech Stack:** PowerShell 7 (`pwsh`, matching the existing `package.ps1`), `fpm` (Ruby gem, "effing package management"), `rpm`/`rpmbuild` (via `apt-get install rpm` on the `ubuntu-latest` runner), ImageMagick's `convert` (preinstalled on `ubuntu-latest`), Docker (preinstalled on `ubuntu-latest`) for the install-check, GitHub Actions (`workflow_dispatch`).

## Global Constraints

- rpm is **additive** — the existing Linux zip artifact is untouched.
- Linux `x86_64` only. New CI steps are gated `if: matrix.os-name == 'linux'`. No arm64 rpm.
- Package identity: name `csp-analyzer`, license `MIT`, url `https://github.com/rubbs14/CSP-Analyzer`.
- Version resolves from `git describe --tags --abbrev=0`, leading `v` stripped (tag `v2.0.1` → rpm version `2.0.1`). Falls back to a `0.0.0-dev`-style value (sanitized, see Task 2) if no tag is reachable — never fails the build over a missing tag.
- Install layout: `/opt/csp-analyzer/**` (full `package.ps1` output), `/usr/bin/csp-analyzer` (wrapper script), `/usr/share/applications/csp-analyzer.desktop`, `/usr/share/icons/hicolor/256x256/apps/csp-analyzer.png`.
- No GUI smoke test of the installed app — no display server in CI. Real app behavior is already covered by `RealDemoDatasetRunTests.cs`.
- This dev box has no `pwsh`, `fpm`, `rpm`/`rpmbuild`, `convert`, or `docker` installed (checked directly). Per the same constraint that applied to `package.ps1` in S14 (`docs/superpowers/plans/2026-07-24-s14-cross-platform-packaging.md` line 1131), the only way to actually exercise this script end-to-end is a real `workflow_dispatch` CI run — that is the test gate for both tasks below, not a local run.

---

## File Structure

- **Create:** `scripts/package/package-rpm.ps1` — builds the rpm staging tree and invokes `fpm`. Single responsibility: turn an already-assembled `package.ps1` output dir into an rpm. Does not itself run `dotnet publish`/`pyinstaller`/`package.ps1`.
- **Modify:** `.github/workflows/ci.yml` — `package` job gains `fetch-depth: 0` on checkout and 6 new steps (install tools, resolve version, build rpm, verify well-formed, verify installs, upload artifact), all gated to the linux leg.

---

### Task 1: `scripts/package/package-rpm.ps1`

**Files:**
- Create: `scripts/package/package-rpm.ps1`

**Interfaces:**
- Consumes: `artifacts/CspAnalyzer-linux-linux-x64/` (the directory `package.ps1 -Rid linux-x64 -OsName linux` produces — same naming convention already established there: `CspAnalyzer-$OsName-$Rid`), and `dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico` (existing asset, confirmed present).
- Produces: CLI contract `./scripts/package/package-rpm.ps1 -Version <string>` (mandatory, no leading `v`, no embedded `-` — see Task 2's sanitization). Writes the result to `artifacts/csp-analyzer-<Version>.x86_64.rpm`. Task 2's CI step calls this exact contract.

- [ ] **Step 1: Write `scripts/package/package-rpm.ps1`**

```powershell
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
# "${iconIco}[0]" (curly braces) rather than "$iconIco[0]" - the latter is
# parsed by PowerShell as string-indexing inside a double-quoted string.
# The "[0]" here is ImageMagick's own frame-select syntax, picking the
# largest frame out of the multi-resolution .ico.
& convert "${iconIco}[0]" -resize 256x256 $iconPngPath
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
```

- [ ] **Step 2: Commit**

```bash
git add scripts/package/package-rpm.ps1
git commit -m "$(cat <<'EOF'
package: add rpm build script (fpm-based, additive to existing zip)

Wraps package.ps1's assembled linux-x64 output into /opt/csp-analyzer +
a /usr/bin wrapper + .desktop entry + icon, then shells out to fpm to
produce the .rpm. Not yet wired into CI - see next commit.
EOF
)"
```

This script cannot be run on this dev box (no `pwsh`/`fpm`/`rpmbuild`/`convert` installed here — see Global Constraints). It is exercised for real in Task 2's CI run, which is the actual test gate for this task too.

---

### Task 2: Wire into `ci.yml`'s `package` job and verify with a real CI run

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `scripts/package/package-rpm.ps1 -Version <string>` (Task 1's exact CLI contract), and the existing `package` job's `matrix.os-name` (`'linux'`/`'windows'`/`'macos'`) / `matrix.rid` variables already used by the "Assemble package" step.
- Produces: a `CspAnalyzer-linux-rpm` artifact on `workflow_dispatch` runs (visible in the Actions run's Artifacts list, same place the existing three zip artifacts already appear).

- [ ] **Step 1: Add `fetch-depth: 0` to the `package` job's checkout**

In `.github/workflows/ci.yml`, inside the `package:` job (the third job, `if: github.event_name == 'workflow_dispatch'`), change:

```yaml
      - uses: actions/checkout@v4
```

to:

```yaml
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
```

(Only this job's checkout — `python-tests` and `dotnet-tests` don't need tags. Needed because `git describe --tags` in Step 2 below requires tag history, and `actions/checkout@v4` defaults to a single-commit shallow clone with no tags.)

- [ ] **Step 2: Add the rpm build/verify/upload steps**

Still in the `package:` job, immediately after the existing `Assemble package` step and its `- uses: actions/upload-artifact@v4` step (the zip upload — leave that step untouched), add:

```yaml
      - name: Install rpm build tools
        if: matrix.os-name == 'linux'
        run: |
          sudo apt-get update
          sudo apt-get install -y rpm
          sudo gem install --no-document fpm

      - name: Resolve rpm version
        if: matrix.os-name == 'linux'
        shell: bash
        run: |
          RAW_VERSION=$(git describe --tags --abbrev=0 2>/dev/null || echo "v0.0.0-dev")
          # rpm's Version field can't contain "-" (reserved for the
          # Version-Release separator) - replace with "." so a fallback
          # "0.0.0-dev" or an annotated tag like "v2.0.1-rc1" both produce
          # a valid rpm version string.
          RPM_VERSION=$(echo "$RAW_VERSION" | sed 's/^v//' | tr '-' '.')
          echo "RPM_VERSION=$RPM_VERSION" >> "$GITHUB_ENV"

      - name: Build rpm package
        if: matrix.os-name == 'linux'
        shell: pwsh
        run: ./scripts/package/package-rpm.ps1 -Version ${{ env.RPM_VERSION }}

      - name: Verify rpm is well-formed
        if: matrix.os-name == 'linux'
        run: |
          rpm -qlp artifacts/*.rpm
          rpm -qip artifacts/*.rpm

      - name: Verify rpm installs cleanly
        if: matrix.os-name == 'linux'
        run: |
          docker run --rm -v ${{ github.workspace }}/artifacts:/artifacts rockylinux:9 bash -c "
            rpm -i /artifacts/*.rpm &&
            test -x /opt/csp-analyzer/CspAnalyzer.Desktop &&
            test -x /usr/bin/csp-analyzer &&
            test -f /usr/share/applications/csp-analyzer.desktop
          "

      - name: Upload rpm artifact
        if: matrix.os-name == 'linux'
        uses: actions/upload-artifact@v4
        with:
          name: CspAnalyzer-linux-rpm
          path: artifacts/*.rpm
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
ci: build and verify an rpm on the linux-x64 package leg

Additive to the existing zip: installs fpm+rpmbuild, resolves a version
from the latest git tag, runs package-rpm.ps1, checks the rpm is
well-formed (rpm -qlp/-qip) and actually installs cleanly inside a
rockylinux:9 container, then uploads it as CspAnalyzer-linux-rpm.
EOF
)"
```

- [ ] **Step 4: Push and manually trigger the `package` workflow**

```bash
git push origin master
```

Then in the GitHub UI: Actions tab → "CI" workflow → "Run workflow" (`workflow_dispatch`) on `master`.

- [ ] **Step 5: Inspect the run — this is the real test gate for Task 1 and Task 2 together**

On the `ubuntu-latest` / linux-x64 leg of the `package` job, confirm:
- "Build rpm package" step succeeds (no thrown error from `package-rpm.ps1`).
- "Verify rpm is well-formed" step's logged `rpm -qlp` output lists `/opt/csp-analyzer/CspAnalyzer.Desktop`, `/usr/bin/csp-analyzer`, `/usr/share/applications/csp-analyzer.desktop`, and the icon path; `rpm -qip` output shows `Name: csp-analyzer`, the resolved version, `License: MIT`.
- "Verify rpm installs cleanly" step exits 0 (all four `test` assertions inside the `rockylinux:9` container pass).
- The run's Artifacts list shows `CspAnalyzer-linux-rpm` alongside the existing `CspAnalyzer-linux-linux-x64`, `CspAnalyzer-windows-win-x64`, `CspAnalyzer-macos-osx-x64` zip artifacts.

If any of these fail, fix `package-rpm.ps1` or the `ci.yml` steps and push a new commit — do not merge/consider this done until a real `workflow_dispatch` run shows all four green.

---

## Self-Review

**Spec coverage:**
- New script wrapping `package.ps1` output → Task 1.
- `/opt`, `/usr/bin` wrapper, `.desktop`, icon (converted from `.ico`) → Task 1, Step 1.
- fpm invocation with name/version/arch/license/description/url → Task 1, Step 1.
- CI gating to `matrix.os-name == 'linux'`, `fetch-depth: 0`, tool install, version resolution (git tag, `v` stripped, fallback), well-formed check, rockylinux install check, separate artifact upload → Task 2.
- Package identity (name `csp-analyzer`, MIT, url) → Task 1, Step 1 (`fpm` args) — matches Global Constraints verbatim.
- Out-of-scope items (deb/AppImage/Flatpak, arm64, GUI smoke test, signing, replacing the zip, a yum repo) — none of them appear in either task; nothing to trim.

**Placeholder scan:** no TBD/TODO; every step has literal file content or literal commands; no "similar to Task N" back-references — Task 2 restates the full YAML block rather than pointing at Task 1's prose.

**Type/name consistency:** `-Version` param name and the `artifacts/csp-analyzer-<Version>.x86_64.rpm` output path match between Task 1's script and Task 2's `Build rpm package` step (`env.RPM_VERSION`) and `Verify rpm is well-formed`/`Upload rpm artifact` steps (`artifacts/*.rpm` glob covers the exact filename Task 1 produces). Directory name `CspAnalyzer-linux-linux-x64` matches the existing `package.ps1`'s `$OsName-$Rid` convention (`os-name: linux`, `rid: linux-x64` from `ci.yml`'s matrix, unchanged by this plan) — verified by re-reading `package.ps1` and the current `ci.yml` matrix before writing Task 1.
