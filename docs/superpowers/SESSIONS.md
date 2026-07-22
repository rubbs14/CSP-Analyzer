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

- [ ] **S7** — .NET 8 Avalonia solution scaffold. App shell + MainWindow layout
  mapping Form1 regions. Builds + runs empty window on Linux.
- [ ] **S8** — Dataset loading: port peaklist(xml)→json pipeline from Form1 C# into
  the new project (MVVM). Unit-test the transform.
- [ ] **S9** — Run flow: invoke python backend via the S6 contract, progress
  reporting, cancel, parse `processed_spectra.json`.
- [ ] **S10** — Results view: tables + charts (replace LiveCharts/WinForms with an
  Avalonia charting approach). Port FormOutputTable.
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
