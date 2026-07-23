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
- [ ] **S10** — Results view: tables + charts (replace LiveCharts/WinForms with an
  Avalonia charting approach). Port FormOutputTable. `MainViewModel.RunResults`
  (populated by S9) is ready to bind.
- [ ] **S11** — Secondary windows (Help, Shortcuts), settings, python/env path
  handling done cross-platform.
- [ ] **S12** — Polish, cross-platform smoke test (Linux + Windows), fix platform gaps.

## Sub-project 6 — CI / packaging

- [ ] **S13** — GitHub Actions: build + test matrix (python + .NET). Cross-platform
  packaging (Linux/Windows/Mac artifacts).

## Notes

- Sessions with heavy compute (env builds, packaging) run background commands.
- Commit only when the user asks; we are on `master` — branch before feature work.
- If a session risks overrunning the token window, stop at the last green checkpoint
  and note remaining steps here.
