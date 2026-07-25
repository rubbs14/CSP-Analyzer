# RPM packaging for Linux release

## Problem

CSP-Analyzer's Linux release artifact (S14, `scripts/package/package.ps1`) is a plain zip: `dotnet publish` output + `model_artifacts/` + PyInstaller-frozen `csp-backend/` dist + `Demo-dataset/`. That's fine for a manual extract-and-run, but it isn't a real Linux package — no `rpm -i`, no app-menu entry, no clean `rpm -e` uninstall. Users on Fedora/RHEL/openSUSE-family distros have no native install path.

Real installers (MSI/dmg/AppImage/deb) were explicitly out of scope in S14 (`docs/superpowers/specs/2026-07-24-sub-project-6-s14-cross-platform-packaging-design.md`). This spec narrows that gap for one specific target: an rpm.

## Decision

**1. New script: `scripts/package/package-rpm.ps1`**

Runs after `package.ps1` has already assembled `artifacts/CspAnalyzer-linux-linux-x64/` (reuses that output rather than re-copying `dotnet publish`/PyInstaller/model_artifacts/demo-dataset itself). Takes `-Version` (required) and builds, into a scratch staging dir:

- `/opt/csp-analyzer/**` ← the entire assembled `package.ps1` output dir, verbatim
- `/usr/bin/csp-analyzer` — wrapper shell script:
  ```sh
  #!/bin/sh
  exec /opt/csp-analyzer/CspAnalyzer.Desktop "$@"
  ```
- `/usr/share/applications/csp-analyzer.desktop`:
  ```ini
  [Desktop Entry]
  Type=Application
  Name=CSP Analyzer
  Exec=csp-analyzer
  Icon=csp-analyzer
  Categories=Science;Chemistry;
  Terminal=false
  ```
- `/usr/share/icons/hicolor/256x256/apps/csp-analyzer.png` — converted from the existing `dotnet/CspAnalyzer.Desktop/Assets/cspanalyzer_SPd_icon.ico` via ImageMagick (`convert cspanalyzer_SPd_icon.ico[0] -resize 256x256 csp-analyzer.png`; `[0]` selects the largest frame in the multi-res `.ico`). `convert` is preinstalled on the `ubuntu-latest` GitHub-hosted runner image.

Then invokes `fpm`:
```
fpm -s dir -t rpm \
  -n csp-analyzer -v <Version> -a x86_64 \
  --license MIT \
  --description "CSP-Analyzer: NMR chemical shift perturbation analysis" \
  --url "https://github.com/rubbs14/CSP-Analyzer" \
  -p artifacts/csp-analyzer-<Version>.x86_64.rpm \
  <staging-dir>/opt/=/opt/ \
  <staging-dir>/usr/=/usr/
```

Fails loudly (`$ErrorActionPreference = "Stop"`, matching `package.ps1`'s style) if `fpm`, `convert`, or `package.ps1`'s output dir aren't present — no silent fallback.

**2. CI wiring (`.github/workflows/ci.yml`, `package` job)**

The `package` job already matrixes over `[ubuntu-latest, windows-latest, macos-latest]` and only runs on `workflow_dispatch`. New steps are added after "Assemble package", gated `if: matrix.os-name == 'linux'`:

- Checkout step for this job gains `fetch-depth: 0` (tags aren't fetched by default; needed for version resolution below). Scoped to the `package` job only — other jobs don't need tags.
- Install build deps: `sudo apt-get update && sudo apt-get install -y rpm` (provides `rpmbuild`, which `fpm -t rpm` shells out to) and `sudo gem install --no-document fpm`.
- Resolve version: `git describe --tags --abbrev=0`, strip a leading `v` (so tag `v2.0.1` → rpm version `2.0.1`, matching `CITATION.cff`'s existing `version: "2.0.1"`). If no tag is reachable, fall back to `0.0.0-dev` rather than failing the build.
- Run `./scripts/package/package-rpm.ps1 -Version <resolved>`.
- **Verify the rpm is well-formed**: `rpm -qlp artifacts/*.rpm` (fails on a corrupt/empty package) and `rpm -qip artifacts/*.rpm` (sanity-check name/version output, not asserted against — just surfaced in the log for a human to eyeball on a `workflow_dispatch` run).
- **Verify the rpm actually installs**, in a real rpm-based container (the `ubuntu-latest` host itself is deb-based and can't `rpm -i` natively):
  ```
  docker run --rm -v ${{ github.workspace }}/artifacts:/artifacts rockylinux:9 bash -c "
    rpm -i /artifacts/*.rpm &&
    test -x /opt/csp-analyzer/CspAnalyzer.Desktop &&
    test -x /usr/bin/csp-analyzer &&
    test -f /usr/share/applications/csp-analyzer.desktop
  "
  ```
- Upload as a separate artifact (`actions/upload-artifact@v4`, `name: CspAnalyzer-linux-rpm`, `path: artifacts/*.rpm`) — additive to the existing zip upload, not a replacement.

**3. Package identity**

- Name: `csp-analyzer`. License: MIT (matches `LICENSE`). Arch: `x86_64` only (matches the existing `linux-x64` rid — no arm64 rpm, consistent with S14 not building arm64 at all).

## Explicitly out of scope

- deb, AppImage, Flatpak, or any non-rpm Linux package format.
- arm64/`linux-arm64` rpm (S14 never built that rid; not adding it here).
- GUI smoke-testing the installed app inside the CI container (no display server; would need Xvfb — real scope creep for a packaging check). The app's actual run behavior is already covered by `RealDemoDatasetRunTests.cs`.
- Code signing / GPG-signing the rpm.
- Replacing or touching the existing Linux zip artifact — rpm is additive.
- A package repo (yum/dnf repo hosting) — the rpm is a downloadable release asset only, same distribution model as the existing zips.

## Verification steps

1. `scripts/package/package-rpm.ps1` runs cleanly against a locally-produced `package.ps1` output dir (requires `fpm` + `rpm` + `convert` installed locally, or run inside a container).
2. `rpm -qlp` on the produced file lists the expected paths (`/opt/csp-analyzer/CspAnalyzer.Desktop`, `/usr/bin/csp-analyzer`, `/usr/share/applications/csp-analyzer.desktop`, the icon).
3. The CI `rockylinux:9` install-and-check step (above) passes: `rpm -i` exits 0, all four `test` assertions pass.
4. A `workflow_dispatch` CI run produces `CspAnalyzer-linux-rpm` as a downloadable artifact alongside the existing three zip artifacts.
