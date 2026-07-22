import csv
import json

import numpy as np
import pytest

from backend.io import class_id_dict_reader, json_constructor, json_parser


def _spectrum(json_data, exp_number, peaks, user_selection=None):
    spectrum = {
        "JSON_Data": json_data,
        "EXP_NUMBER": exp_number,
        "PEAKLIST": [
            {"F1": f1, "F2": f2, "INTENSITY": intensity}
            for f1, f2, intensity in peaks
        ],
    }
    if user_selection is not None:
        spectrum["UserSelection"] = user_selection
    return spectrum


def test_json_parser_splits_reference_and_experiment(tmp_path):
    data = [
        _spectrum("Reference", 0, [(1.0, 2.0, 50000.0)]),
        _spectrum("Experiment", 5, [(3.0, 4.0, 50000.0)]),
    ]
    json_file = tmp_path / "input.json"
    json_file.write_text(json.dumps(data))

    result = json_parser(str(json_file), intensity_threshold=20000)

    np.testing.assert_array_equal(result["Reference"], [[1.0, 2.0, 50000.0]])
    np.testing.assert_array_equal(result["Experiment"][5], [[3.0, 4.0, 50000.0]])


def test_json_parser_filters_peaks_below_intensity_threshold(tmp_path):
    data = [
        _spectrum(
            "Reference", 0, [(1.0, 2.0, 50000.0), (9.0, 9.0, 10.0)]
        ),
    ]
    json_file = tmp_path / "input.json"
    json_file.write_text(json.dumps(data))

    result = json_parser(str(json_file), intensity_threshold=20000)

    np.testing.assert_array_equal(result["Reference"], [[1.0, 2.0, 50000.0]])


def test_json_parser_accepts_bare_object_not_wrapped_in_array(tmp_path):
    data = _spectrum("Reference", 0, [(1.0, 2.0, 50000.0)])
    json_file = tmp_path / "input.json"
    json_file.write_text(json.dumps(data))

    result = json_parser(str(json_file), intensity_threshold=20000)

    np.testing.assert_array_equal(result["Reference"], [[1.0, 2.0, 50000.0]])


def test_json_parser_debug_mode_returns_activity_dict(tmp_path):
    data = [
        _spectrum(
            "Experiment", 7, [(1.0, 2.0, 50000.0)], user_selection="ACTIVE (MAN)"
        ),
        _spectrum(
            "Experiment", 8, [(1.0, 2.0, 50000.0)], user_selection="INACTIVE"
        ),
    ]
    json_file = tmp_path / "input.json"
    json_file.write_text(json.dumps(data))

    peaklists, activity = json_parser(
        str(json_file), intensity_threshold=20000, debug_mode=True
    )

    assert activity == {7: 1, 8: 0}


def test_class_id_dict_reader_maps_activity_2_to_1(tmp_path):
    csv_file = tmp_path / "ids.csv"
    with open(csv_file, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["123X", "2"])

    result = class_id_dict_reader(str(csv_file))

    assert result == {123: 1}


def test_class_id_dict_reader_leaves_activity_1_unchanged(tmp_path):
    csv_file = tmp_path / "ids.csv"
    with open(csv_file, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["55Y", "1"])

    result = class_id_dict_reader(str(csv_file))

    assert result == {55: 1}


def test_class_id_dict_reader_leaves_activity_0_unchanged(tmp_path):
    csv_file = tmp_path / "ids.csv"
    with open(csv_file, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["9Z", "0"])

    result = class_id_dict_reader(str(csv_file))

    assert result == {9: 0}


def test_json_constructor_emits_plain_bool_not_one_tuple(tmp_path):
    out_file = tmp_path / "out.json"

    json_constructor(probas=[0.8, 0.2], labels=[1, 2], dump_location=str(out_file))

    written = json.loads(out_file.read_text())

    assert written == [
        {"EXP_NUMBER": 1, "isActive": True, "activePseudoprobability": 0.8},
        {"EXP_NUMBER": 2, "isActive": False, "activePseudoprobability": 0.2},
    ]


def test_json_constructor_boundary_at_0_5_is_active(tmp_path):
    out_file = tmp_path / "out.json"

    json_constructor(probas=[0.5], labels=[1], dump_location=str(out_file))

    written = json.loads(out_file.read_text())

    assert written[0]["isActive"] is True
