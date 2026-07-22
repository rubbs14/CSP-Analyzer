import os
import sys

import numpy as np

from backend.classify import DEFAULT_MODEL_DIR, load_pipeline, nmr_classification, proba_ranker
from backend.features import comparator_function, compute_reference_features, reference_spectrum, spectrum_process
from backend.io import json_constructor, json_parser

INTENSITY_THRESHOLD = 20000
BINS_PER_ARRAY_DIMENSION = 500


def _feature_matrix(experiment_peaklists, sorted_keys, bins_per_array_dimension, reference_features, reference_threshold):
    return np.asarray(
        [
            comparator_function(
                spectrum_process(experiment_peaklists[key], bins_per_array_dimension, reference_threshold),
                reference_features,
            )
            for key in sorted_keys
        ]
    )


def run(
    json_location,
    output_location=None,
    model_dir=DEFAULT_MODEL_DIR,
    intensity_threshold=INTENSITY_THRESHOLD,
    bins_per_array_dimension=BINS_PER_ARRAY_DIMENSION,
):
    if output_location is None:
        output_location = os.path.dirname(json_location) or "."

    ref_exp_dict = json_parser(json_location, intensity_threshold)

    if len(ref_exp_dict["Reference"]) == 0:
        raise ValueError(f"No reference spectrum found in '{json_location}'")

    reference_hist, reference_threshold = reference_spectrum(
        ref_exp_dict["Reference"], bins_per_array_dimension
    )
    reference_features = compute_reference_features(reference_hist)

    sorted_keys = sorted(ref_exp_dict["Experiment"].keys())
    feature_matrix = _feature_matrix(
        ref_exp_dict["Experiment"],
        sorted_keys,
        bins_per_array_dimension,
        reference_features,
        reference_threshold,
    )

    scaler, pca, svm = load_pipeline(model_dir)
    _reduced_data, probas = nmr_classification(feature_matrix, scaler, pca, svm)
    probas, labels = proba_ranker(probas, sorted_keys)

    output_path = os.path.join(output_location, "processed_spectra.json")
    json_constructor(probas, labels, output_path)
    return output_path


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv

    if not argv:
        print("Usage: python -m backend <json_in> [out_dir]", file=sys.stderr)
        return 1

    json_location = argv[0]
    output_location = argv[1] if len(argv) > 1 else None

    try:
        output_path = run(json_location, output_location)
    except (ValueError, RuntimeError, FileNotFoundError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    print(f"Predictions made, saved to file at {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
