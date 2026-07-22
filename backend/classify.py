import os

import numpy as np

from backend.model_io import load_pca, load_scaler, load_svc

DEFAULT_MODEL_DIR = "model_artifacts"


def load_pipeline(model_dir=DEFAULT_MODEL_DIR):
    """Load the fitted scaler/PCA/SVM trio from `model_dir` (npz+json, see
    `backend/model_io.py`). Raises `FileNotFoundError` if `model_dir` doesn't
    contain the expected artifact files. `model_dir` should be produced by
    `backend/scripts/migrate_legacy_pickles.py` (or `model_io.save_*`
    directly) - never point it at a pickle file or an untrusted directory:
    see the module docstring in `model_io.py` for why pickle was dropped."""
    try:
        scaler = load_scaler(os.path.join(model_dir, "scaler"))
        pca = load_pca(os.path.join(model_dir, "pca"))
        svm = load_svc(os.path.join(model_dir, "svm"))
    except FileNotFoundError:
        raise
    except (KeyError, ValueError, OSError) as exc:
        raise RuntimeError(
            f"Failed to load model artifacts from '{model_dir}': {exc}. "
            f"Expected scaler/pca/svm .npz+.json files as produced by "
            f"backend/scripts/migrate_legacy_pickles.py."
        ) from exc

    return scaler, pca, svm


def nmr_classification(feature_matrix, scaler, pca, svm):
    scaled_data = scaler.transform(feature_matrix)
    reduced_data = pca.transform(scaled_data)
    probas = svm.predict_proba(reduced_data)[:, 1]
    return reduced_data, probas


def proba_ranker(probas, labels):
    probas = np.asarray(probas)
    labels = np.asarray(labels)
    sorted_indices = np.argsort(probas)[::-1]
    return probas[sorted_indices], labels[sorted_indices]
