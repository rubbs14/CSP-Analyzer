import numpy as np
import pytest

from backend.classify import load_pipeline, nmr_classification, proba_ranker
from backend.tests.helpers import build_synthetic_model_dir, build_synthetic_pipeline


def test_load_pipeline_returns_fitted_scaler_pca_svm(tmp_path):
    model_dir = build_synthetic_model_dir(tmp_path / "model_artifacts")

    scaler, pca, svm = load_pipeline(str(model_dir))

    assert hasattr(scaler, "transform")
    assert hasattr(pca, "transform")
    assert hasattr(svm, "predict_proba")


def test_load_pipeline_missing_file_raises_file_not_found(tmp_path):
    with pytest.raises(FileNotFoundError):
        load_pipeline(str(tmp_path / "does_not_exist"))


def test_load_pipeline_wraps_corrupt_artifact_with_actionable_message(tmp_path):
    model_dir = build_synthetic_model_dir(tmp_path / "model_artifacts")
    # Truncate the json sidecar so it's present but unparseable/incomplete -
    # a stand-in for "artifact format doesn't match what model_io expects".
    (model_dir / "svm.json").write_text("{}")

    with pytest.raises(RuntimeError, match="migrate_legacy_pickles"):
        load_pipeline(str(model_dir))


def test_nmr_classification_returns_probability_of_positive_class():
    scaler, pca, svm = build_synthetic_pipeline()

    feature_matrix = np.zeros((2, 31))
    feature_matrix[1] = 3.0  # nudge toward the class-1 cluster mean

    _reduced, probas = nmr_classification(feature_matrix, scaler, pca, svm)

    assert probas.shape == (2,)
    assert (probas >= 0).all() and (probas <= 1).all()


def test_proba_ranker_sorts_probas_descending_and_reorders_labels():
    probas, labels = proba_ranker([0.2, 0.9, 0.5], [10, 20, 30])

    np.testing.assert_array_equal(probas, [0.9, 0.5, 0.2])
    np.testing.assert_array_equal(labels, [20, 30, 10])
