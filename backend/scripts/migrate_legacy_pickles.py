"""One-off migration: legacy scikit-learn 0.19.1 pickles -> safe npz/json artifacts.

Run once, offline, against the trusted, repo-committed `CSPv2/pickle_jar/`
files (S4). Never run this against an untrusted pickle. Output goes to
`backend/model_artifacts/`, which `classify.load_pipeline` reads at runtime
via `backend/model_io.py` -- no pickle involved after this script has run.

`affprop_transform.pkl` (AffinityPropagation) is not migrated: it is not
referenced anywhere in `backend/` or in either legacy `Backend.py` /
`NMR_classifier_production.py` script, so it is dead weight left over from
an earlier iteration of the model.

Usage: python -m backend.scripts.migrate_legacy_pickles
"""

import os
import pickle
import sys

import numpy as np

_REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
_LEGACY_PICKLE_JAR = os.path.join(_REPO_ROOT, "CSPv2", "pickle_jar")
_OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "model_artifacts")


def _install_legacy_module_aliases():
    """The legacy pickles reference scikit-learn 0.19.1 module paths that
    were later renamed/removed (e.g. `sklearn.svm.classes` ->
    `sklearn.svm._classes`). Aliasing lets `pickle.load` resolve the classes;
    scikit-learn's own `__setstate__` version-mismatch warning still fires
    and is expected."""
    import sklearn.decomposition
    import sklearn.preprocessing
    import sklearn.svm

    sys.modules["sklearn.preprocessing.data"] = sklearn.preprocessing
    sys.modules["sklearn.decomposition.pca"] = sklearn.decomposition
    sys.modules["sklearn.svm.classes"] = sklearn.svm


def _load_legacy(filename):
    path = os.path.join(_LEGACY_PICKLE_JAR, filename)
    with open(path, "rb") as f:
        return pickle.load(f)


def main():
    from backend.model_io import save_pca, save_scaler, save_svc

    _install_legacy_module_aliases()

    os.makedirs(_OUT_DIR, exist_ok=True)

    scaler = _load_legacy("fit_transform.pkl")
    save_scaler(scaler, os.path.join(_OUT_DIR, "scaler"))

    pca = _load_legacy("pca_transform.pkl")
    save_pca(pca, os.path.join(_OUT_DIR, "pca"))

    svm = _load_legacy("prediction_model.pkl")
    save_svc(svm, os.path.join(_OUT_DIR, "svm"))

    print(f"Migrated scaler/pca/svm -> {_OUT_DIR}")


if __name__ == "__main__":
    main()
