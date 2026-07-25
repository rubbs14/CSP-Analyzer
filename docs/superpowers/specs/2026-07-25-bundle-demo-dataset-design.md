# Bundle demo dataset for onboarding/testing

**Date**: 2026-07-25
**Status**: approved

## Problem

`CSPv2/Demo-dataset` (65 files, 1.3M) was stripped from git tracking in S5
(2026-07-22) alongside the 505M bundled Miniconda3 and the pickle jar, for
repo hygiene. Local copies were kept but nothing ships it anymore:

- Dev checkouts: a fresh `git clone` has no sample data to load/try the app
  with, unlike sessions S8-S10 which tested against the local copy.
- Release packages: `scripts/package/package.ps1` (used by the CI `package`
  workflow_dispatch job) assembles the downloadable zip from `dotnet publish`
  output + `model_artifacts/` + the PyInstaller-frozen `csp-backend/` dist —
  no sample data. A user who downloads a release zip has no dataset to try
  CSP-Analyzer with until they bring their own TopSpin experiment folder.

## Decision

Re-track `CSPv2/Demo-dataset` in git and ship it in both places:

1. **Git**: remove the `CSPv2/Demo-dataset/` line from `.gitignore`, re-add
   the files. At 1.3M this doesn't carry the same hygiene concern as the
   505M Miniconda3 tree — no size/hygiene tradeoff worth debating. Full
   dataset (not a trimmed subset) is re-tracked, since it's what S8/S9/S10's
   manual verification already exercised (83 ref peaks / 64 experiments) and
   there's no meaningful repo-size cost to keeping it whole.
2. **Package**: `scripts/package/package.ps1` copies `CSPv2/Demo-dataset`
   into the assembled package output (same pattern as the existing
   `model_artifacts`/`csp-backend` copies), so it's present in the zip a
   release-package user downloads and extracts.
3. **README**: one-line pointer under "Getting Started" telling users the
   bundled dataset exists and can be loaded via the existing Load
   Reference/Load Dataset pickers — no new UI.
4. **CI**: a real full-run integration test against the actual dataset,
   in `dotnet-tests`. Today `BackendInterop.Tests`' integration tests
   already shell out to a real python backend (`BackendEnvironment.
   PythonExecutable`), but only look for a conda env literally named
   `csp_modern` — never present on GitHub-hosted runners, so those tests
   silently self-skip in CI today and have never actually run there.
   Fix: add a `CSP_ANALYZER_PYTHON` environment-variable override, checked
   before the conda-path guess, and set it in the `dotnet-tests` CI job
   after a `pip install -r backend/requirements.txt` step (mirroring
   `python-tests`' own setup) so `PythonExecutable` resolves to a real
   interpreter in CI too. Add a new test,
   `CspAnalyzer.Desktop.Tests/RealDemoDatasetRunTests.cs`, that drives
   `MainViewModel` (`LoadReferenceCommand` → `LoadDatasetCommand` →
   `RunCommand`) against the real `CSPv2/Demo-dataset` folders — reusing
   the project's existing `FixedFolderFilePickerService` fake-picker
   pattern already used by `MainViewModelNavigationTests` — self-skipping
   the same way the BackendInterop integration tests do when no python is
   resolvable. Assertions pin the already-verified real numbers from S8-S10's
   manual testing: 83 reference peaks, 64 dataset experiments, 64 run
   results, 1 active / 63 inactive (`ActivesAutoCount`/`InactivesAutoCount`).

Location stays `CSPv2/Demo-dataset` (not moved to a new top-level path):
the legacy `CSPv2/` WinForms dir still exists pre-cutover, no app code
hardcodes this path, and S8/S9/S10's already-verified manual test runs used
this exact path.

## Explicitly out of scope

- No new "Load Sample Data" shortcut/command — reuse the existing pickers.
- No SESSIONS.md session entry — this is an ad-hoc addition, same pattern
  as the 2026-07-23 UI-parity pass that also wasn't S-numbered.
- No change to `package.ps1`'s existing error/validation style beyond
  mirroring what it already does for `model_artifacts`.

## Verification

- `git status` shows `CSPv2/Demo-dataset` tracked, `.gitignore` no longer
  excludes it.
- Manual `pwsh scripts/package/package.ps1 -Rid <rid> -OsName <name>` (or a
  code read confirming the added copy step matches the existing ones) shows
  the dataset present in `artifacts/CspAnalyzer-*/`.
- README renders the new note in the right section.
- `dotnet test dotnet/CspAnalyzer.sln` passes locally with
  `CSP_ANALYZER_PYTHON` unset (existing self-skip behavior unchanged) and,
  separately, with it set to the local `csp_modern` python (new test
  actually runs and its assertions hold against the real dataset).
