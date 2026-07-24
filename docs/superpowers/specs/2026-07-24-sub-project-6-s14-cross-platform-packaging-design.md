# S14 — Cross-platform packaging + install/paper docs — Design

Sub-project 6 (CI / packaging), session 2 of 2 (after S13's test CI). Produces
downloadable, self-contained CSP-Analyzer builds for Linux/Windows/Mac, and
brings the README's docs up to date for the packaged app and its source paper.

## Problem

`BackendEnvironment.RepoRoot`/`ModelDir`/`PythonExecutable` (S9/S11) only work
in a dev checkout: `RepoRoot` requires a `.git` folder next to `backend/`,
and `PythonExecutable` probes for a `csp_modern` conda env the end user won't
have. Neither holds in a packaged install. There's also no build step that
produces an installable artifact at all, and the README predates this whole
`.NET 8 + Avalonia` rewrite — its install-adjacent instructions (Troubleshooting
→ "No Python.exe found") still describe manually copying a Miniconda3 folder,
and there's no synopsis of the paper this tool implements beyond a bare
citation block.

## Decisions (from brainstorming)

1. **Python shipping**: bundle a standalone python. PyInstaller freezes
   `backend/` (+ numpy/scipy/scikit-learn/scikit-image) into a self-contained
   per-OS executable. No conda/env setup required by the end user.
2. **Packaged-mode layout**: everything lives under the app's own install
   directory (siblings of the executable) — `model_artifacts/` and the frozen
   backend dist are copied alongside the `dotnet publish` output. Resolution
   becomes `AppContext.BaseDirectory`-relative, no `.git` walk-up needed for
   packaged installs.
3. **Mode detection**: probe first. `BackendEnvironment` checks for the
   packaged layout under `AppContext.BaseDirectory`; if absent, falls back to
   today's `.git` walk-up + conda-env probe (dev checkout). One code path,
   no build-time flag, no env var.
4. **Artifact format**: self-contained `dotnet publish -r <RID>
   --self-contained`, zipped per OS. Not real installers (no MSI/dmg/AppImage/
   deb) — that's a materially bigger scope, left for a future session if ever
   needed.
5. **CI trigger**: `workflow_dispatch` only (manual). Packaging is heavy
   (native per-OS PyInstaller freeze, large uploads) and this project has no
   release cadence yet; keep S13's push/PR CI fast and unaffected.
6. **Docs**: install instructions + an expanded paper synopsis land in
   `README.md` as part of this session's deliverable (not deferred).

## Architecture

```
CI job "package" (workflow_dispatch, matrix: ubuntu-latest/windows-latest/macos-latest)
  ├─ pip install -r backend/requirements.txt + pyinstaller
  ├─ pyinstaller: freeze `python -m backend` entry point → dist/csp-backend/ (native per OS, no cross-compile)
  ├─ dotnet publish CspAnalyzer.Desktop -r <RID> --self-contained -o publish/
  ├─ package script assembles:
  │     publish/                      (app exe + .NET deps)
  │     publish/model_artifacts/      (copied from backend/model_artifacts)
  │     publish/csp-backend/          (PyInstaller dist, copied)
  └─ zip publish/ → CspAnalyzer-<os>-<rid>.zip, uploaded as a workflow artifact
```

RID matrix: `win-x64`, `linux-x64`, `osx-x64`. (arm64/Apple Silicon and code
signing are out of scope — noted below.)

## Component changes

### `BackendCliRunner` (breaking signature change)

Today `Run`/`RunAsync` take a raw `pythonExecutable: string` and always
prepend `-m backend` to the argument list — this assumes the target is a
python interpreter. A frozen PyInstaller executable *is* the backend
entrypoint already; invoking it with a leading `-m backend` would be wrong.

New type:

```csharp
public sealed record BackendExecutable(string FileName, IReadOnlyList<string> LeadingArgs);
```

- Dev mode: `new BackendExecutable(pythonPath, ["-m", "backend"])`.
- Packaged mode: `new BackendExecutable(frozenExePath, [])`.

`BackendCliRunner.Run`/`RunAsync`/`BuildStartInfo` take a `BackendExecutable`
instead of `pythonExecutable: string`, and `LeadingArgs` are added to
`ArgumentList` before `jsonIn` (replacing the hardcoded `"-m"`/`"backend"`
adds). All existing call sites (`MainViewModel`, `BackendInterop.Tests`)
update to construct the appropriate `BackendExecutable`.

### `BackendEnvironment`

- New: `IsPackagedLayout` — true when `AppContext.BaseDirectory` directly
  contains both `model_artifacts/` and a recognizable frozen-backend dist
  (e.g. `csp-backend/csp-backend` or `csp-backend/csp-backend.exe`).
- `ModelDir`: `AppContext.BaseDirectory`-relative when `IsPackagedLayout`,
  else today's repo-root-relative path.
- `Executable` (replaces the old `PythonExecutable` as the thing callers
  consume): returns a `BackendExecutable` — packaged mode points at the
  frozen dist directly; dev mode keeps the existing `CondaPythonPaths`
  probe + `-m backend`.
- `RepoRoot`/`FindRepoRoot()` untouched, still dev-only, only consulted on
  the fallback path.

### Packaging script

A new script (exact form decided in the implementation plan — likely a
small shell/PowerShell-agnostic approach or a `dotnet` MSBuild target,
whichever proves simplest per-OS in CI) performs the "assemble + zip" step
after `pyinstaller` and `dotnet publish` both complete. Lives under
`scripts/package/` or similar; not fully specified here, left to the plan.

### CI (`.github/workflows/ci.yml`)

New job, e.g. `package`, `workflow_dispatch`-triggered, matrixed like S13's
existing jobs. Uploads the per-OS zip via `actions/upload-artifact`.

## Known risk (flag, not solved here)

PyInstaller freezing packages with heavy dynamic-import behavior
(scikit-learn's dispatch, scikit-image's plugin loading) is a known-finicky
combination — hidden-imports may need to be added explicitly, and the first
freeze attempt on each OS is likely to fail in a way that needs a real build
to diagnose (same class of gotcha as S7's Avalonia-12-template issue or
S13's case-collision bug — verify by actually running the freeze, not by
reasoning about it in advance).

## Documentation changes (`README.md`)

1. **New "Installation" section** (placed after "Overview", before "How It
   Works" or "Getting Started" — exact position decided during
   implementation): per-OS instructions for downloading a packaged zip,
   extracting, and running the executable directly. No install wizard, no
   manual Miniconda/pickle_jar copying.
2. **Troubleshooting → "No Python.exe found"**: rewritten to reflect that
   the backend now ships bundled inside the package; the section either
   drops or is replaced with packaged-mode-specific guidance (e.g. "don't
   move the executable out of its extracted folder").
3. **New short "Background" section**, placed just above the existing
   "Citation" section (which is kept as-is), synopsizing the paper this tool
   implements:
   - Motivation: manual review of hundreds of 2D NMR spectra in
     fragment-based screening is slow and inconsistent between sessions
     ("human bias" from fatigue/ordering effects).
   - Method: each HSQC spectrum reduced to a 15-element descriptor vector
     (HOG, phase cross-correlation registration, ORB point-matching,
     structural similarity, Hu moments, MSE/PSNR, Jensen-Shannon entropy)
     compared against a reference spectrum; SMOTE-ENN class balancing → PCA
     → SVC (RBF kernel) with Platt scaling for calibrated probabilities.
   - Validation: 1,611 2D HSQC spectra across 4 protein targets; 100-spectrum
     training set (32 confirmed actives + inactives/noise); average accuracy
     0.87, sensitivity 0.72, specificity 0.88, false-negative rate 3.10%,
     false-positive rate 10.30% — tuned to favor missing fewer true actives
     over rejecting more false ones.
   - Source: Fino, Byrne, Softley, Sattler, Schneider & Popowicz (2020),
     *Computational and Structural Biotechnology Journal* 18, 603–611.
     (Already cited below; this section is prose context, not a duplicate
     citation.)

## Testing / verification

- `dotnet test CspAnalyzer.sln` after the `BackendCliRunner`/
  `BackendEnvironment` changes — existing suite plus new unit tests for
  `BackendExecutable` construction in both modes and the packaged-layout
  probe (using a temp directory fixture, no real PyInstaller build needed
  for unit-level coverage).
- Real end-to-end verification requires an actual CI run of the new
  `package` job (can't fully verify PyInstaller freezing without it — this
  dev box can build for Linux locally but not Windows/macOS). Plan should
  call for triggering it and checking results via `gh run view`, matching
  S13's verification pattern.
- README changes: manual proofread; no automated doc tests in this repo.

## Out of scope

- Real per-OS installers (MSI, .app/.dmg, AppImage/deb).
- Code signing / notarization.
- arm64/Apple Silicon builds (osx-arm64) — x64 only for now (works via
  Rosetta on Apple Silicon, not native).
- Auto-update mechanism.
- CI trigger beyond manual dispatch (e.g. tag-triggered releases) — can be
  added later without redesign if a release cadence emerges.
