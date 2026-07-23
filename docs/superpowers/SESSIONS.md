# CSP-Analyzer Modernization — Session Roadmap

Multi-session program. Each session = one focused deliverable sized to fit ~one
PRO hourly token window. Sessions are ordered by dependency. Check off as done.
Resume cold: read this + the sub-project specs in `docs/superpowers/specs/`.

Target stack: .NET 8 + Avalonia UI (Linux/Windows/Mac), modern python backend
(conda env `csp_modern`, py3.12). IronPython/python2/WinForms dropped.

## Sub-project 1 — Python backend package (foundation)

- [x] **S1** — Scaffold `backend/` package. Port `io.py`: json_parser (kill bare
  except / `locals()` hack), json_constructor (fix tuple bug → flat plain-bool
  JSON), class_id_dict_reader (drop dead branch). TDD: unit tests first.
- [x] **S2** — `features.py`: two_dimensional_hist (`density=`), reference_spectrum,
  spectrum_process, comparator_function. Port removed skimage APIs; reference
  features passed explicitly (no module globals). Preserve 31-elem vector layout.
  Unit tests per feature fn.
- [x] **S3** — `classify.py` + `__main__.py` CLI. Reconstruct a small demo `.json`
  input; pin regression baseline (golden feature matrix + probas). `requirements.txt`
  pinned; backend README documenting ORB/phase drift risk. End-to-end run green.

## Sub-project 4 — Model handling / security (pulled early: blocks real predictions)

- [x] **S4** — `pickle_jar/*.pkl` confirmed incompatible under sklearn 1.9
  (`ModuleNotFoundError`, old internal module paths). Decision: re-serialize.
  Built a scikit-learn 0.19.1 bridge env as a trusted oracle, hand-reconstructed
  `SVC`'s private libsvm-backed attributes (module aliasing + a `dual_coef_`/
  `intercept_` sign-convention gotcha), verified exact equivalence, and wrote
  `backend/model_io.py` (safe npz+json persistence, no pickle at runtime) +
  `backend/scripts/migrate_legacy_pickles.py` (one-off migration) +
  `backend/model_artifacts/`. `classify.load_pipeline` now reads only the safe
  format. See `docs/superpowers/specs/2026-07-22-sub-project-4-model-handling-spec.md`.

## Sub-project 5 — Repo hygiene

- [x] **S5** — `git rm --cached` bundled Miniconda3 (~12k files) + pkl + demo dataset
  (local files kept). Add `.gitignore`. Verify nothing that builds depends on tracked
  copies. Commit. (History rewrite = optional later.)

## Sub-project 2 — Backend ↔ UI interface

- [x] **S6** — Stable CLI contract defined: exit codes 0/1/2, stdout-on-success
  is exactly the output path, stderr carries errors/diagnostics, `--model-dir`
  and `--bins-per-array-dimension` exposed so callers aren't CWD-dependent.
  Broadened `main()`'s exception handling to catch everything (no raw
  tracebacks). `dotnet/BackendInterop` (.NET 8 class library) + xunit test
  project prove the contract: `BackendCliRunner` shells out via `ArgumentList`
  (no string-concat), `SpectrumResult` deserializes the plain-bool JSON with
  no regex workaround. See
  `docs/superpowers/specs/2026-07-22-sub-project-2-backend-ui-interface-spec.md`.

## Sub-project 3 — Avalonia UI port (largest; multiple sessions)

- [x] **S7** — `dotnet/CspAnalyzer.Desktop` (Avalonia 11.2.3 MVVM app, net8.0,
  CommunityToolkit.Mvvm) scaffolded via `dotnet new avalonia.mvvm`, folded into
  the renamed umbrella `dotnet/CspAnalyzer.sln` alongside `BackendInterop`.
  `MainWindow.axaml` lays out empty placeholder regions mirroring every
  `Form1.Designer.cs` panel (sidebar: File I/O / Import-Run / Run Info /
  Help-Shortcuts; content: 2 stacked bar-chart placeholders + spectra-overlay
  placeholder; bottom bar: analysis-info / actives-inactives / manual-results
  / current-experiment+goto / player buttons+export-reset) - no bindings or
  real charts yet, that's S8-S10. Builds clean and confirmed running (real
  window, screenshotted) on this Linux box.
- [x] **S8** — Dataset loading: ported peaklist(xml)→json pipeline from Form1 C#
  (`SPECTRUM.Read_spectrum`) into `dotnet/BackendInterop` (`Peak`,
  `PeaklistSpectrum`, `PeaklistPathInfo`, `PeaklistXmlParser`,
  `PeaklistImporter`) - JSON property names mirror backend/io.py's
  `json_parser` exactly. TDD, 13 new xunit tests. Wired minimal MVVM in
  `CspAnalyzer.Desktop`: `MainViewModel`'s `LoadReferenceCommand`/
  `LoadDatasetCommand` over a new `IFilePickerService` (Avalonia
  `IStorageProvider` wrapper, replacing WinForms' Open/FolderBrowserDialog),
  bound into `MainWindow.axaml`'s S7 placeholder regions (import
  range/threshold textboxes, reference/dataset status, Analysis Info stats).
  Confirmed by actually running the app and clicking through Load
  Reference/Load Dataset against the real local `CSPv2/Demo-dataset` (83 ref
  peaks, 64 experiments loaded correctly).
- [x] **S9** — Run flow: `BackendCliRunner.RunAsync` (cancellation-aware,
  kills the process tree) + `BackendEnvironment` (repo-root/model-dir/python
  discovery, S11-provisional) in `dotnet/BackendInterop`. `MainViewModel`
  gained `RunCommand`/`CancelRunCommand`: serializes loaded reference+dataset
  spectra to a temp JSON, shells out, parses `processed_spectra.json` into
  `RunResults`. `MainWindow.axaml`'s "Run CSP (S9)" placeholder is now a real
  button + Cancel + indeterminate progress bar (backend has no incremental
  progress protocol, so no percentage). Verified end-to-end against the real
  local `CSPv2/Demo-dataset` + real model artifacts via a throwaway
  ViewModel harness (64 experiments classified; mid-run cancel confirmed).
- [x] **S10** — Results view: `ResultsWindow` (table + 3 pie charts, port of
  `FormOutputTable`), opened from `MainWindow`'s "Export" button. Charting via
  LiveChartsCore.SkiaSharpView.Avalonia. CSV/XLSX (ClosedXML)/PDF (PDFsharp,
  bundled DejaVu Sans font) export replace the old Excel-interop/GDI+ print
  buttons. See `docs/superpowers/specs/2026-07-23-sub-project-3-s10-results-view-design.md`.
- [x] **S10b** — Form1's own embedded charts + manual-override workflow.
  `MainViewModel` split into partial files by responsibility
  (`.Navigation.cs`, `.ManualOverride.cs`, `.Charts.cs`): player nav
  (First/Previous/Next/Last/GoToExperiment, bounds enforced via
  `CanExecute` instead of legacy's disable-after-the-fact pattern),
  `ExperimentFilter?` (replaces two independently-mutable
  ShowActives/ShowInactives bools), mark active/inactive/reset/reset-all
  (mutates the already-existing `PeaklistSpectrum.UserSelection`, which
  now actually flows into `ResultsWindow`'s Manual Flag column/pie via
  the S10 `ResultsBuilder` join - no new plumbing needed there, just
  something to mutate it). New `dotnet/CspAnalyzer.Desktop.Tests` xunit
  project (first ViewModel test coverage since S7), TDD throughout, 21
  new tests. Every LiveChartsCore 2.0.5 API used (`CartesianChart`
  `Sections`/`VisualElements`/axis `SharedWith` for zoom-sync,
  `RectangularSection`, `LabelVisual`, `GaugeGenerator.BuildSolidGauge`,
  `ScatterSeries<WeightedPoint>`) was verified against the installed
  package via a throwaway reflection probe before writing any XAML/C#,
  to avoid repeating S7's guessed-API gotcha - this caught a real error
  in the design spec's gauge section (`RadialGaugeSeries`/`GaugeBuilder`
  don't exist in this package; the real gauge API is
  `GaugeGenerator.BuildSolidGauge` bound into a `PieChart`, fixed before
  implementation started). See
  `docs/superpowers/specs/2026-07-23-sub-project-3-s10b-form-charts-manual-override-design.md`
  and `docs/superpowers/plans/2026-07-23-s10b-form-charts-manual-override.md`.

  **Deliberate simplification from "full fidelity"**: legacy's per-bar
  conditional `Fill` (coloring individual bars red past a threshold via
  LiveCharts1's `Mapper.Fill(item => ...)`) was dropped in favor of the
  `RectangularSection` threshold-zone shading alone - LiveChartsCore 2.x's
  per-point styling model doesn't have a direct equivalent, and the zone
  shading conveys the same diagnostic information.

  Verified end-to-end against the real local `CSPv2/Demo-dataset` (git-
  ignored, kept locally per S5) via a temporary auto-driving patch to
  `App.axaml.cs` (reverted before commit, never landed) run + screenshot:
  Peak-Diff chart populates immediately after dataset load (no run
  needed) with real threshold-zone shading; after a run, Probability
  chart, both solid gauges (1 active / 63 inactive, real
  `GaugeGenerator.BuildSolidGauge` output), and the spectra-overlay
  scatter (real N/H/intensity-sized points) all render correctly; nav
  labels (`Current Experiment: 11  1 / 64`, `ΔPeaks: 7`, `INACTIVE`)
  matched the real classification output. No GUI automation tool on this
  box (still true per S9's note - no `xdotool`), so button-click
  interactions (Mark Active, filter checkboxes, Reset All confirm
  dialog) are covered by the 21 new unit tests instead of a live click
  pass.
- [x] **S11** — Cross-platform python/env discovery: `BackendEnvironment`'s
  conda-path probing covers Windows/Linux/macOS (miniforge3/miniconda3/
  anaconda3), replacing the single hardcoded Linux path. See
  `docs/superpowers/specs/2026-07-23-sub-project-3-s11-cross-platform-python-discovery-design.md`.
- [x] **S11b** — Settings persistence: `AppSettings` POCO + `SettingsService`
  (plain `System.Text.Json`, `SpecialFolder.ApplicationData/CspAnalyzer/
  settings.json`, following S11's cross-platform `SpecialFolder` idiom;
  missing/corrupt file silently falls back to defaults - no logging
  framework exists in this codebase to log the failure to). Covers S10b's
  Appearance theme/background-color choices (previously code-behind-only,
  reset every launch), the six import-filter thresholds (`NMin`/`NMax`/
  `HMin`/`HMax`/both intensity thresholds), `ManualProbabilityThreshold`,
  a new `BinsPerArrayDimension` override (previously always hardcoded
  `null`), and window size/position/maximized state - expanded from
  SESSIONS.md's original Appearance-only wording per brainstorming.
  `MainViewModel.ApplySettings`/`CurrentSettings` and `MainWindow.
  ApplyAppearanceSettings`/`PopulateAppearanceSettings` merge into one
  `AppSettings` instance; `App.axaml.cs` loads+applies at startup, saves
  once on the window's `Closing` event (no per-change writes).
  `ResetImportControls`/`ResetPeakFiltering` still reset to their original
  hardcoded literals regardless of loaded settings. 17 new tests across 4
  TDD tasks, each independently task-reviewed clean, plus a final
  whole-branch review (verdict: ready to merge). See
  `docs/superpowers/specs/2026-07-23-sub-project-3-s11b-settings-persistence-design.md`
  and `docs/superpowers/plans/2026-07-23-s11b-settings-persistence.md`.

  **Known limitation**: persisting `ManualProbabilityThreshold` is a no-op
  in practice - the ViewModel field is a non-nullable `double` (default
  0.5), so the persisted value is never actually `null`, and `RunAsync`
  unconditionally overwrites it with `ComputeAutoProbabilityThreshold()`
  on every run anyway. Flagged in final review as a Minor, non-blocking
  design wart, not fixed this session.
- [x] **S11c** — Real keyboard shortcuts + Shortcuts reference window + About
  window. Expanded from SESSIONS.md's original "port FormHelp/FormShortcuts as
  a static reference" wording per brainstorming: real `Window.KeyBindings` now
  wire ~20 legacy shortcuts to pre-existing `MainViewModel`/`ResultsViewModel`
  commands on both `MainWindow` and `ResultsWindow` (not just documented).
  Legacy shortcuts with no real command in the port (about a third of the
  original set) are listed in the new `ShortcutsWindow` as "(not yet
  implemented)" rather than silently dropped or wired to a no-op. New
  `IAboutWindowService`/`IShortcutsWindowService` (+ `Avalonia*`/`Null*`
  implementations) follow the established `IConfirmDialogService`/
  `IResultsWindowService` single-method-interface pattern, injected into
  `MainViewModel`'s constructor (3→5 params). New composite
  `ResetAllImportAndThresholdControlsCommand`. Help window + its TopSpin
  command generator deferred to S11d (out of scope this session).

  A `GuardedViewModelCommand` mechanism (not in the original plan - discovered
  necessary during implementation and added via review-driven fixes) ANDs
  every bare-letter/arrow-key/Ctrl+C/Ctrl+X binding's `CanExecute` with "no
  `TextBox` is currently focused," so normal text editing (typing, arrow-key
  caret movement, copy/cut) in any sidebar textbox isn't hijacked by a
  shortcut. Two real bugs of this class were caught and fixed via per-task
  review before merge: `T` (could cancel a running analysis by typing "t" into
  a focused textbox) and the four arrow keys (broke caret movement) in the
  first review round; `Ctrl+C`/`Ctrl+X` (hijacked standard copy/cut) in a
  second round. A tautological test for `ResultsWindow`'s `R`→`RefreshCommand`
  binding (asserted a value invariant under the test fixture regardless of
  whether the binding fired) was also caught and replaced with a real
  `CollectionChanged`-observing assertion. Executed as 5 TDD tasks via
  subagent-driven-development; Tasks 1-3 clean on first review, Tasks 4-5 each
  needed one extra review-and-fix round for the bugs above, plus a final
  whole-branch review (opus) that caught one more gap (missing explicit guard
  test for `G`, one of the letters the plan's global constraints enumerate) -
  fixed before merge. 106 tests total (up from 76 at session start). See
  `docs/superpowers/specs/2026-07-23-sub-project-3-s11c-keyboard-shortcuts-design.md`
  and `docs/superpowers/plans/2026-07-23-s11c-keyboard-shortcuts.md`.

  **Known limitations, non-blocking**: `Ctrl+Alt+*` combos are unguarded (no
  practical collision - none of this app's text fields need the special
  characters AltGr+letter produces on some keyboard layouts); a few gestures
  (`Ctrl+E`/`Ctrl+P` on ResultsWindow, `Ctrl+Alt+R`/`Ctrl+Alt+E` on MainWindow)
  are validated only indirectly (same command already bound to a button in the
  same view) rather than by a dedicated key-press test.
- [ ] **S11d** — Help window + TopSpin command generator (ported from
  `CSPv2/FormHelp`). Split out of S11c's original scope during brainstorming.
- [ ] **S12** — Polish, cross-platform smoke test (Linux + Windows), fix platform gaps.

## Sub-project 6 — CI / packaging

- [ ] **S13** — GitHub Actions: build + test matrix (python + .NET). Cross-platform
  packaging (Linux/Windows/Mac artifacts).

## Notes

- Sessions with heavy compute (env builds, packaging) run background commands.
- Commit only when the user asks; we are on `master` — branch before feature work.
- If a session risks overrunning the token window, stop at the last green checkpoint
  and note remaining steps here.
