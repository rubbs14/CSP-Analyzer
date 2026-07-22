# Sub-project 4 — Model handling / security (S4)

Status: **done** (2026-07-22).

## Problem

`CSPv2/pickle_jar/{fit_transform,pca_transform,prediction_model}.pkl`
(StandardScaler / PCA / SVC, trained under scikit-learn 0.19.1 + Python 3.6)
are the only real trained model the app has. They block real predictions two
ways:

1. **Don't load at all** under current scikit-learn — `pickle.load` raises
   `ModuleNotFoundError` (old internal module paths like `sklearn.svm.classes`
   were renamed/removed by later scikit-learn versions).
2. **`pickle.load` is arbitrary code execution** even when it works — the
   original `classify.load_pipeline` unpickled these files directly at
   runtime.

`CSPv2/pickle_jar/affprop_transform.pkl` (`AffinityPropagation`) is also
present but was confirmed **unused** — not referenced by `backend/`, by
`CSPv2/NMR_classifier_production.py`, or by `Backend/Backend.py`. Left alone,
not migrated.

## Decision

**Re-serialize**, not retrain-from-scratch or export to ONNX. No training
data is available in the repo (only the fitted pickles), so retraining was
never an option. ONNX was considered as the more "future-proof" choice, but
re-serializing to a safe, minimal format was simpler, needed no new runtime
dependency (`onnxruntime`), and — once the scikit-learn-version bridging
problem was solved anyway (see below) — gave an equally pickle-free result.

**Approach**: a one-off migration script
(`backend/scripts/migrate_legacy_pickles.py`) unpickles the three legacy
files exactly once, offline, against the trusted repo-committed input, and
re-saves their fitted parameters as plain numpy arrays (`.npz`,
`allow_pickle=False`) + a small JSON sidecar of hyperparameters, under
`backend/model_artifacts/`. `backend/model_io.py` provides the save/load
functions; `classify.load_pipeline` calls only the `load_*` side at runtime.
No pickle is involved after migration has run once — non-negotiable given
`load_pipeline` runs against every peaklist the app ever processes.

## How the legacy pickles were actually made to load

Two independent problems, solved separately:

**1. Module path resolution.** The legacy files reference module paths
scikit-learn has since renamed (`sklearn.preprocessing.data` →
`sklearn.preprocessing._data`, `sklearn.decomposition.pca` →
`sklearn.decomposition._pca`, `sklearn.svm.classes` → `sklearn.svm._classes`).
Aliasing `sys.modules` before unpickling (`migrate_legacy_pickles.py:
_install_legacy_module_aliases`) resolves this — scikit-learn's own
`__setstate__` version-mismatch warning still fires (expected, harmless).

**2. `StandardScaler`/`PCA` "just worked" after that** — their `transform()`
only depends on plain numpy-array attributes (`mean_`, `scale_`,
`components_`, ...) that have been stable across the whole 0.19.1 → 1.9 span.
Verified numerically identical to a scikit-learn 0.19.1 bridge env (below) to
float noise.

**3. `SVC` needed hand reconstruction.** Current scikit-learn's `SVC` reads
`predict`/`predict_proba` inputs from *private*, underscore-prefixed mirror
attributes (`_dual_coef_`, `_intercept_`, `_probA`, `_probB`, `_n_support`,
`_gamma`) that a `.fit()` call populates alongside the public ones — the
legacy pickle only has the public ones. Three gotchas, in the order they
were found (all documented again in `model_io.py`'s docstrings, next to the
code):
   - `n_support_`, `probA_`, `probB_` are **read-only properties** on
     current `SVC`, backed by the private mirrors. A legacy unpickled object
     still has the old public values sitting in `__dict__` (unpickling
     bypasses the setter-less property), invisible to normal attribute
     access but readable via `obj.__dict__["probA_"]` etc.
   - `_gamma` must be resolved from the `gamma` hyperparameter manually
     (`1/n_features` for `"auto"`, the only string mode resolvable without
     the original training data — `"scale"` needs `X`'s variance, not
     persisted). The real model uses `"auto"`.
   - **The sign-convention gotcha** (most dangerous — a wrong-but-plausible
     result, not a crash): the internal `_dual_coef_`/`_intercept_` scikit-learn
     reads at predict time are the *negation* of the public
     `dual_coef_`/`intercept_` for binary classification, regardless of
     scikit-learn version (both 0.19.1 and 1.9 apply this same flip
     internally). Getting the sign wrong doesn't error — it silently
     produces a mirror-image decision function (confirmed by direct
     experiment: unflipped gave `decision_function` values exactly negated
     vs. the trusted reference, which then still looked like "a valid
     probability" downstream). `load_svc` sets
     `_dual_coef_ = -dual_coef_`, `_intercept_ = -intercept_`.

## Equivalence verification

A scikit-learn 0.19.1 / Python 3.6 conda-forge "bridge" environment
(`conda create -n csp_bridge -c conda-forge python=3.6 scikit-learn=0.19.1
numpy scipy` + `libgfortran=3` for an old shared-lib dependency) was built
purely as a trusted oracle — it loads the legacy pickles natively, with no
guesswork. Not part of the ongoing toolchain; not committed anywhere.

Checked against it, from current scikit-learn (`csp_modern`, sklearn 1.9.0):

- `StandardScaler.transform` / `PCA.transform`: exact match (`np.allclose`,
  `rtol=1e-8`/`1e-6`) on random synthetic input.
- `SVC.predict_proba` / `.decision_function` / `.predict`: exact match to
  float noise (~`1e-14` max abs diff) on 80 points jittered around real
  support vectors, spanning **both** classes (`pred` distribution: 32/48).
  This is what caught the sign-convention bug — random out-of-distribution
  input alone saturated the decision function far from the margin and
  produced identical-looking (but wrong-reasoned) results either way, so the
  jittered-around-real-support-vectors test was necessary, not just
  convenient.

`backend/tests/test_model_io.py` keeps a pinned regression check (fixed,
non-random support vector indices) so a future scikit-learn upgrade or
`model_io.py` change that silently reintroduces this class of bug fails
loudly, without needing the bridge env (which isn't part of this repo's
toolchain going forward).

## Consequences for `backend/`

- `classify.DEFAULT_PICKLE_JAR` / `pickle_jar_dir` renamed to
  `DEFAULT_MODEL_DIR` / `model_dir` throughout (`classify.py`, `__main__.py`,
  `run()`'s public signature) — "pickle jar" was actively misleading once
  pickle left the runtime path.
- `backend/tests/helpers.py`: `build_synthetic_pipeline` now returns
  in-memory objects (no file I/O) for wiring tests that don't care about file
  format; `build_synthetic_model_dir` (new) exercises `load_pipeline`'s real
  npz/json file-handling contract via a tiny freshly-fit `SVC`.
- `test_end_to_end.py`'s golden-baseline test now runs against the real
  `model_artifacts/`, not a synthetic fixture — S3's synthetic-fixture
  workaround for "the real model can't load yet" is gone; the golden JSON was
  regenerated (probabilities changed, since the model itself changed from
  "fake" to "real" — still saturated, since the demo dataset is
  out-of-distribution for it, see `backend/README.md`).

## Deferred / out of scope

- `CSPv2/pickle_jar/*.pkl` (and the bundled `Miniconda3/pickle_jar/*.pkl`
  copy) are left in place, untouched, as the historical source of truth for
  the migration. Sub-project 5 (S5, repo hygiene) decides what happens to
  the bundled `Miniconda3/` tree; the `pickle_jar/*.pkl` files themselves are
  small and arguably worth keeping tracked as provenance for
  `model_artifacts/`, but that's S5's call, not re-litigated here.
- `affprop_transform.pkl` was not migrated (confirmed dead — see above). Not
  deleted either; that's also an S5 hygiene call.
