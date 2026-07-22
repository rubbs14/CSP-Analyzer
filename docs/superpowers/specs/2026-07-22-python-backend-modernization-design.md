# CSP-Analyzer — Python Backend Modernization (Design)

Date: 2026-07-22
Status: Approved (design)

## Context

CSP-Analyzer is a WinForms C# (.NET 4.5.2) GUI that shells out to a bundled
Windows Miniconda3 `py36_csp` env to run an NMR-spectrum classifier. The
classifier extracts image-like features from 2-D NMR peak histograms and feeds
them to a pickled scaler → PCA → SVM pipeline.

The Python backend is broken on any modern environment: it imports skimage/scipy
APIs that were removed years ago and can only run inside the frozen py36 env. The
env bundled in the repo is Windows-only and cannot run on the Linux dev box, so
there is **no runnable oracle** for the original feature values.

Two near-duplicate scripts exist and have diverged:
- `Backend/Backend.py`
- `CSPv2/NMR_classifier_production.py` (the one the GUI actually invokes)

## Program context (REVISED — full cross-platform rewrite)

The overall goal changed after initial design: modernize the WHOLE app onto a
cross-platform stack — **.NET 8 + Avalonia UI** running on Linux, Windows, and Mac
(the dev box is Linux, so WinForms is dropped entirely), with IronPython 2.7 and
python2 removed. This is a multi-session program, decomposed into sub-projects:

1. **Python backend package + CLI + tests** ← THIS spec (foundation, unblocked).
2. Backend ↔ UI interface (shell-out CLI vs local API).
3. Avalonia UI port of the WinForms forms.
4. Model handling / security (pickles → possibly ONNX).
5. Repo hygiene / git strip of bundled Miniconda + pkl + dataset.
6. Cross-platform CI + packaging.

Each later sub-project gets its own spec. Backend goes first because the UI and
interface decisions depend on a clean, testable backend contract.

## Scope (this sub-project)

In scope:
- Fix confirmed bugs.
- Port removed skimage/scipy APIs to current equivalents (feature-preserving math).
- Collapse the two duplicate scripts into one clean, tested package with a stable
  CLI contract the future Avalonia UI can call.
- Drop the dead theano/keras code path.
- Strip bundled Miniconda3 + `.pkl` models + demo dataset from git tracking; add
  `.gitignore` + pinned `requirements.txt`.

Out of scope (this sub-project — handled in later sub-projects):
- Avalonia UI port and .NET 8 migration.
- No model retraining.
- No git history rewrite (untrack only; history rewrite is a later optional step).

## Confirmed Bugs

1. **Tuple bug** — `NMR_classifier_production.py:244` `exp_result = float(proba) >= 0.5,`
   trailing comma makes `isActive` a 1-tuple `(True,)`, serialized as `[true]`,
   which forces a regex hack in `Form1.cs:1522-1523`. Fix: emit a plain bool;
   remove the C# regex hack (small C# edit, allowed as it is required for the fix).
2. **Diverged duplicates** — Backend.py uses `.append` (nested list); classifier
   uses `.extend` (flat). C# expects flat. Collapse to one module.
3. **Removed APIs** — `compare_ssim`, `compare_nrmse`, `compare_psnr`,
   `register_translation`, `histogram2d(normed=...)`.
4. **Dead branch** — `class_id_dict_reader`: `if int(activity)==1: pass`.
5. **Fragile parse** — bare `except:` + `if 'js' not in locals()` control flow.
6. **Global mutable state** — `ref_hog`/`ref_ent`/... module globals combined with
   multiprocessing (Backend.py) go stale across runs. New module recomputes per run
   or passes reference features explicitly.

## API Port Mapping (feature-preserving)

| Old | New |
|-----|-----|
| `skimage.measure.compare_ssim` | `skimage.metrics.structural_similarity` |
| `skimage.measure.compare_nrmse` | `skimage.metrics.normalized_root_mse` |
| `skimage.measure.compare_psnr` | `skimage.metrics.peak_signal_noise_ratio` |
| `skimage.feature.register_translation` | `skimage.registration.phase_cross_correlation` |
| `np.histogram2d(..., normed=False)` | `np.histogram2d(..., density=False)` |
| `ssim(multichannel=True)` on 2-D grayscale | 2-D call (no channel arg) |

`moments_hu`, `hog`, `ORB` still exist; keep. Preserve the exact scalar layout of
the 31-element (`11 + n_key`, n_key=20) feature vector and its index assignments.

## Equivalence Strategy (approved: "new baseline + document risk")

The old env is unrunnable, so we cannot diff against original outputs. Therefore:
- Treat the modernized libraries as the new source of truth.
- Unit-test each feature function for shape, dtype, determinism, and known-value
  sanity (identical inputs → zero distance; entropy of constant array; etc.).
- Capture a **regression baseline**: run the full pipeline on a reconstructed demo
  input and pin the resulting feature matrix / probabilities as golden files;
  future changes diff against these.
- **Documented risk**: `phase_cross_correlation` and `ORB` may differ internally
  from the versions the pickled model was trained on, so absolute predictions may
  shift versus the original. Record this prominently in the backend README.

## Target Module Structure

Replace both scripts with a package (proposed `backend/` at repo root; final path
confirmed in the plan to match how the C# GUI invokes it):

- `features.py` — `two_dimensional_hist`, `reference_spectrum`, `spectrum_process`,
  `comparator_function`. Reference features passed explicitly (no module globals).
- `io.py` — `json_parser`, `json_constructor` (flat, plain-bool output),
  `class_id_dict_reader`.
- `classify.py` — load pkl artifacts, scale → PCA → `predict_proba`, `proba_ranker`.
- `__main__.py` — CLI: `argv[1]=json_in`, optional `argv[2]=out_dir`; wires the above.
- `requirements.txt` — pinned modern numpy/scipy/scikit-image/scikit-learn.
- `tests/` — pytest unit + regression tests.

Each module is independently testable with a narrow interface. The GUI keeps calling
one entry point (script path + json arg), so the C# change is limited to (a) the
new script path if it moves and (b) removing the now-unneeded regex hack.

## Data Flow (unchanged externally)

`demo peaklists → C# builds <ds>.json → python __main__ → features → classify →
processed_spectra.json → C# deserializes → GUI tables/charts`

Pickle loading remains (security caveat noted), but loaded only from the app's own
`pickle_jar/`.

## Testing

- pytest for python: per-feature unit tests + one end-to-end regression test against
  pinned golden output on a small reconstructed demo json.
- Manual C#: confirm GUI still parses the cleaned JSON (no regex hack) — verified in
  a later session on Windows; noted as a cross-session follow-up.

## Risks

- Feature drift vs original-trained model (ORB/phase) — accepted, documented.
- Reconstructing a valid demo `.json` input (peaklists → json) needed for the
  regression baseline; if the C# json shape is non-trivial, derive it from
  `json_parser`'s expected keys (`JSON_Data`, `PEAKLIST`, `F1/F2/INTENSITY`,
  `EXP_NUMBER`, `UserSelection`).
- Untracking bundled files must not delete users' local env (`git rm --cached`).
