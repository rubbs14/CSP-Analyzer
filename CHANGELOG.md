# Changelog

All notable changes to CSP-Analyzer are documented here. This project follows semantic versioning; see `git tag` for the complete release history.

## [Unreleased]

### Added

- **Windows installer** — Inno Setup-based per-user installer alongside existing zip artifacts; includes proper version detection for upgrades
- **Linux RPM packaging** — fpm-based RPM package for Linux distributions, installable via package managers
- **Demo dataset bundled** — includes demo-dataset in repo for onboarding and real-dataset CI coverage

### Fixed

- Windows installer: icon shortcut handling and upgrade robustness
- RPM packaging: icon frame and binary permission handling (chmod +x)
- CI robustness: script injection vulnerability in rpm build step, `CSP_ANALYZER_PYTHON` path resolution

---

## [2.0.1] - 2026-07-25

### Added

- **App icon refresh** — new B1 logo with animated banner (plays once on startup, holds final frame)
- **Citation metadata** — `CITATION.cff` file for GitHub's "Cite this repository" button
- **Zenodo archival** — `.zenodo.json` metadata for correct software archive registration; Zenodo software DOI badge and concept DOI in README
- **ELIXIR bio.tools registry** — `biotools.json` with biotoolsID/biotoolsCURIE for discovery

### Changed

- README: improved readability, fixed stale installation/backend claims, added badges (platform, .NET version, paper DOI/open-access)
- README: added folder-structure diagram for data loading

### Fixed

- HelpWindow clipboard test: fixed race condition in async void (was intermittently flaky in CI)

---

## [2.0.0] - 2026-07-25

### Added

**Python backend (`backend/` package)**
- Complete modern Python 3.12 backend with TDD unit tests
- Ported from legacy script with bug fixes: removed bare `except` clauses, fixed JSON tuple-serialization bug, eliminated dead code branches
- Stable CLI contract: exit codes (0/1/2), stdout for success output, stderr for errors, `--model-dir` and `--bins-per-array-dimension` flags

**Model persistence**
- Safe npz+json model format replacing legacy pickle (no pickle at runtime)
- Migration tool for re-serializing legacy pickled models; verified numerical equivalence to original scikit-learn 0.19 artifacts
- Robust model discovery across conda environments

**Desktop UI (Avalonia 11 MVVM, .NET 8)**
- Complete rewrite from legacy WinForms (Windows-only) to cross-platform Avalonia
- Now runs on **Linux, Windows, and macOS** (x64)
- Dataset loading: recursive TopSpin `peaklist.xml` parsing with import filter thresholds (N/H intensity/count ranges)
- Analysis run flow: end-to-end CSP classification with live progress feedback and cancel support
- Full results view with interactive charts and export

**Charts & visualization**
- Peak-difference chart (bar chart with threshold-zone shading)
- Probability distribution chart
- Active/inactive gauges (visual summary of classification results)
- Spectrum-overlay scatter plot (real-time zoom/pan)
- CSV, XLSX (ClosedXML), and PDF (PDFsharp + DejaVu Sans font) export options

**Interactivity & settings**
- ~20 legacy keyboard shortcuts ported and fully wired
- Manual override workflow: mark spectra as active/inactive and see results update live
- Settings persistence: theme/background-color, import filter thresholds, window size/position/maximized state
- Keyboard-aware shortcuts: guarded to prevent hijacking text-input editing (copy/cut/paste/arrow-key caret movement)
- Help window with interactive TopSpin command generator (numeric validation, copy-to-clipboard)
- About window with AEGIS funding attribution

**Testing & CI**
- GitHub Actions CI matrix: Python + .NET tests across Linux, Windows, macOS
- Self-contained cross-platform release artifacts (Linux/Windows/macOS zip packages)
- Model artifacts bundled with desktop package (no separate installation needed)

### Changed

- **From WinForms to Avalonia:** Complete UI architecture rewrite; Windows-only constraint lifted
- **From pickle to safe formats:** Legacy pickled SVM models re-serialized to npz+json (scikit-learn 1.9+ compatible)
- **Python backend:** Bare `except` exceptions now explicitly caught; removed module-global state (reference features passed explicitly)
- **Removed legacy artifacts:** ~12k bundled Miniconda3 files now in `.gitignore` (not tracked in git)

### Fixed

- JSON tuple serialization: legacy backend incorrectly serialized tuples as JSON arrays; now emits plain booleans
- SVM dual_coef_ sign convention: re-serialization corrected scikit-learn 0.19→1.9 compatibility issue via module aliasing bridge
- Cross-platform CI failure: a stray legacy `Backend/` folder (dead since early in the rewrite) collided on case-insensitive macOS/Windows filesystems with the real `backend/` package, causing `ModuleNotFoundError` in CI; removed the dead folder
- Keyboard shortcuts hijacking text input: single-letter and arrow-key shortcuts (e.g. `T`, arrow keys, Ctrl+C/Ctrl+X) were firing while typing in a focused textbox instead of editing the text; shortcuts now yield to text-input focus
- Zoom-reset shortcuts swapped: `Ctrl+C`/`Ctrl+Y` were bound to each other's legacy behavior (bar-chart reset vs. overlay-chart reset); corrected to match the original app

---

## [1.0] - 2019-12-11

### Added

- Original CSP-Analyzer release: WinForms/.NET Framework desktop app with Python 2 IronPython backend
- Machine-learning-based classification for NMR fragment screening spectra
- Integration with TopSpin peak-picking workflow
