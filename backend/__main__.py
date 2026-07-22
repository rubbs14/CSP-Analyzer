import argparse
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


def _build_parser():
    parser = argparse.ArgumentParser(prog="python -m backend")
    parser.add_argument("json_in", help="Peaklist JSON (reference + experiment spectra)")
    parser.add_argument("out_dir", nargs="?", default=None, help="Defaults to json_in's directory")
    parser.add_argument("--model-dir", dest="model_dir", default=DEFAULT_MODEL_DIR)
    parser.add_argument(
        "--bins-per-array-dimension", dest="bins_per_array_dimension", type=int, default=BINS_PER_ARRAY_DIMENSION
    )
    return parser


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv

    parser = _build_parser()
    try:
        args = parser.parse_args(argv)
    except SystemExit as exc:
        # argparse already printed usage/error to stderr (or --help to
        # stdout) and picked its own exit code (2 for a bad argv, 0 for
        # --help) - propagate it as-is rather than forcing our own.
        return exc.code if exc.code is not None else 0

    try:
        output_path = run(
            args.json_in,
            args.out_dir,
            model_dir=args.model_dir,
            bins_per_array_dimension=args.bins_per_array_dimension,
        )
    except Exception as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    # Contract: on success, stdout is *exactly* the output path (one line,
    # nothing else) so a calling process can read it without parsing prose.
    # Human-readable status goes to stderr instead.
    print(f"Predictions made, saved to file at {output_path}", file=sys.stderr)
    print(output_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
