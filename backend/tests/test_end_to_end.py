import json
import os

import numpy as np
import pytest

from backend.__main__ import run
from backend.features import comparator_function, compute_reference_features, reference_spectrum, spectrum_process
from backend.tests.helpers import DEMO_BINS, block_image, build_demo_json, peaklist_from_image

GOLDEN_DIR = os.path.join(os.path.dirname(__file__), "golden")
REAL_MODEL_DIR = os.path.join(os.path.dirname(__file__), "..", "model_artifacts")


def _load_golden(name):
    with open(os.path.join(GOLDEN_DIR, name)) as f:
        return json.load(f)


def test_demo_feature_matrix_matches_golden_baseline():
    # Regression pin for features.py: if a future skimage/scipy port shifts
    # the numeric output, this fails loudly instead of silently drifting.
    reference_peaks = peaklist_from_image(block_image(offset=0))
    reference_hist, threshold = reference_spectrum(reference_peaks, DEMO_BINS)
    reference_features = compute_reference_features(reference_hist)

    rows = []
    for offset in (2, 4):
        peaks = peaklist_from_image(block_image(offset=offset))
        hist = spectrum_process(peaks, DEMO_BINS, threshold)
        rows.append(comparator_function(hist, reference_features).tolist())

    golden = _load_golden("demo_feature_matrix.json")
    np.testing.assert_allclose(rows, golden, rtol=1e-6, atol=1e-9)


def test_end_to_end_cli_run_matches_golden_baseline(tmp_path):
    # Full io -> features -> classify -> json_constructor pipeline, using the
    # real S4-migrated model_artifacts/ (see backend/model_io.py and
    # backend/scripts/migrate_legacy_pickles.py). The demo dataset is a
    # synthetic 128x128 block-grid image, not a real spectrum, so it's
    # off-distribution for this model - the pinned probabilities are a
    # reproducibility regression pin, not a claim about real accuracy.
    json_path = build_demo_json(str(tmp_path / "demo.json"))

    output_path = run(json_path, str(tmp_path), model_dir=REAL_MODEL_DIR, bins_per_array_dimension=DEMO_BINS)

    with open(output_path) as f:
        result = json.load(f)

    golden = _load_golden("demo_processed_spectra.json")

    # Compared by EXP_NUMBER, not list position, since proba_ranker's sort
    # order isn't the thing this test is pinning - that's covered directly
    # by test_classify.py::test_proba_ranker_sorts_probas_descending_and_reorders_labels.
    result_by_id = {r["EXP_NUMBER"]: r for r in result}
    golden_by_id = {g["EXP_NUMBER"]: g for g in golden}

    assert result_by_id.keys() == golden_by_id.keys()
    for exp_number, golden_record in golden_by_id.items():
        record = result_by_id[exp_number]
        assert record["isActive"] == golden_record["isActive"]
        assert record["activePseudoprobability"] == pytest.approx(
            golden_record["activePseudoprobability"], rel=1e-6
        )


def test_run_raises_on_missing_reference_spectrum(tmp_path):
    json_path = tmp_path / "no_reference.json"
    json_path.write_text(
        json.dumps(
            [
                {
                    "JSON_Data": "Experiment",
                    "EXP_NUMBER": 1,
                    "PEAKLIST": [{"F1": 100.0, "F2": 8.0, "INTENSITY": 50000.0}],
                }
            ]
        )
    )

    with pytest.raises(ValueError, match="No reference spectrum"):
        run(str(json_path), str(tmp_path))
