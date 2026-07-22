# backend

Modernized python replacement for `Backend/Backend.py` and
`CSPv2/NMR_classifier_production.py`. Takes a peaklist JSON (one reference
spectrum + N experiment spectra), extracts image-like comparison features
from each experiment against the reference, and classifies each as
active/inactive via a pickled scaler -> PCA -> SVM pipeline.

See `docs/superpowers/specs/2026-07-22-python-backend-modernization-design.md`
for the full modernization design and rationale.

## Modules

- `io.py` — `json_parser` (peaklist JSON -> reference/experiment arrays),
  `json_constructor` (results -> flat, plain-bool JSON), `class_id_dict_reader`.
- `features.py` — `two_dimensional_hist`, `reference_spectrum`,
  `spectrum_process`, `compute_reference_features`, `comparator_function`.
  Produces the 31-element feature vector (`FEATURE_VECTOR_LENGTH`) the
  pickled models were trained on.
- `classify.py` — `load_pipeline` (load scaler/PCA/SVM from `model_artifacts/`
  via `model_io.py`), `nmr_classification` (scale -> PCA -> `predict_proba`),
  `proba_ranker`.
- `model_io.py` — safe (non-pickle) npz+json persistence for the fitted
  scaler/PCA/SVM, and the scikit-learn-version-bridging logic for the SVM
  (see "Real model now loads" below).
- `scripts/migrate_legacy_pickles.py` — one-off migration:
  `CSPv2/pickle_jar/*.pkl` (scikit-learn 0.19.1) -> `model_artifacts/`
  (npz+json). Already run; re-run only if the legacy pickles or the
  `model_io.py` format change.
- `__main__.py` — CLI wiring: `run()` (library entry point) and `main()`
  (argv parsing).

## CLI usage

```
python -m backend <json_in> [out_dir] [--model-dir DIR] [--bins-per-array-dimension N]
```

`out_dir` defaults to `json_in`'s directory. Writes `processed_spectra.json`
there. `--model-dir` defaults to `model_artifacts/` relative to the current
working directory - pass it explicitly (an absolute path) from any caller
that doesn't control its own CWD, e.g. the .NET UI. Full stable contract
(exit codes, stdout/stderr split, JSON schema) for the future Avalonia UI's
call layer is defined in sub-project 2:
`docs/superpowers/specs/2026-07-22-sub-project-2-backend-ui-interface-spec.md`.

## Known quirks (preserved intentionally)

- **`spectrum_process` leaves a gap unbinarized**: values in
  `[1, reference_threshold)` satisfy neither `>= reference_threshold` nor
  `< 1`, so they pass through un-binarized. Present in both original
  duplicate scripts; baked into the pickled models' training data, so it's
  kept as-is rather than "fixed". See
  `tests/test_features.py::test_spectrum_process_leaves_values_between_one_and_threshold_unbinarized`.
- **ORB / `phase_cross_correlation` drift risk**: these skimage APIs may
  compute slightly differently internally versus the versions the pickled
  models were originally trained against (API renamed, internals unverified
  across the version gap - see the design doc's API Port Mapping table).
  Absolute predictions from the real `pickle_jar` models may shift versus
  the original app. Accepted per the "new baseline + document risk" strategy;
  not fixable without retraining.
- **Real model now loads, via `model_artifacts/` (S4)**: the committed
  `CSPv2/pickle_jar/*.pkl` files don't unpickle at all under current
  scikit-learn (`ModuleNotFoundError` — old internal module paths like
  `sklearn.svm.classes` were removed/renamed) — confirmed during S4. Rather
  than depend on `pickle.load` (arbitrary code execution) or on scikit-learn's
  pickle format surviving a 0.19.1 -> 1.9 jump, S4 wrote a one-off migration
  (`backend/scripts/migrate_legacy_pickles.py`) that unpickles the legacy
  files exactly once (via a module-alias shim + hand-reconstructing SVC's
  private libsvm-backed attributes — see `model_io.py`'s docstrings for the
  details, especially the `dual_coef_`/`intercept_` sign-convention gotcha)
  and re-saves the fitted parameters as plain `.npz` (`allow_pickle=False`)
  + `.json` under `backend/model_artifacts/`. `classify.load_pipeline` reads
  only that safe format at runtime; no pickle is involved after migration.
  Verified against a scikit-learn 0.19.1 bridge conda env: `predict_proba`/
  `decision_function` on the migrated model matched the original to float
  noise (~1e-14) across both classes. `affprop_transform.pkl`
  (`AffinityPropagation`) was *not* migrated — grep confirms it's unreferenced
  by any current or legacy code path, a dead artifact from an earlier model
  iteration.
- **SVM `gamma` must be `'auto'` (or numeric) to survive the safe format**:
  `model_io.load_svc` resolves `gamma="auto"` as `1/n_features` without
  needing the original training data; `gamma="scale"` (current scikit-learn's
  default) needs `X`'s variance at fit time, which isn't part of the
  persisted format, and raises `ValueError` if seen. Not a real constraint —
  the actual pickled model (and every synthetic SVC test fixture) uses
  `gamma="auto"`.

## Tests

`pytest backend/tests` (see `requirements-dev.txt`). Since S4, `test_end_to_end.py`'s
`test_end_to_end_cli_run_matches_golden_baseline` runs against the *real*
`model_artifacts/` (migrated from `CSPv2/pickle_jar/`), exercising the full
`io -> features -> classify -> json_constructor` pipeline end-to-end with
the production model. The demo dataset is still a synthetic 128x128
grid-of-blocks pattern (`tests/helpers.py:build_demo_json`), not a real
spectrum, so it's off-distribution for the model and the pinned probability
happens to be saturated — that's expected, this is a reproducibility pin,
not a claim about real-world accuracy. `test_model_io.py` separately pins
`predict_proba` for fixed (non-random) real support vectors, as a more
direct regression check on `model_io.py` itself. `test_classify.py` keeps a
LogisticRegression-based synthetic fixture
(`tests/helpers.py:build_synthetic_pipeline`) for wiring tests that don't
care about the real model's numbers.

`tests/golden/` holds the pinned baseline (feature matrix + final probas)
for the synthetic demo dataset. If a future skimage/scipy/sklearn upgrade
shifts these numbers, these tests will fail and need the golden files
regenerated deliberately (not just re-run) after confirming the shift is
expected.

`tests/conftest.py` pins BLAS to a single thread for the whole test session
so the golden-baseline float comparisons are reproducible across machines/thread
counts.
