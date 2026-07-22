import json
import os

from backend.__main__ import main
from backend.tests.helpers import DEMO_BINS, build_demo_json, build_synthetic_model_dir

REAL_MODEL_DIR = os.path.join(os.path.dirname(__file__), "..", "model_artifacts")


def test_main_with_no_args_exits_2_with_usage_on_stderr(capsys):
    exit_code = main([])

    assert exit_code == 2
    captured = capsys.readouterr()
    assert captured.out == ""
    assert "usage" in captured.err.lower()


def test_main_with_nonexistent_json_exits_1_with_clean_error_on_stderr(capsys):
    exit_code = main(["/no/such/file.json"])

    assert exit_code == 1
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err.startswith("Error: ")
    # No raw traceback: a single clean line, not multiple frames.
    assert captured.err.count("\n") == 1


def test_main_happy_path_stdout_is_exactly_the_output_path(tmp_path, capsys):
    json_path = build_demo_json(str(tmp_path / "demo.json"))

    exit_code = main(
        [
            json_path,
            str(tmp_path),
            "--model-dir",
            REAL_MODEL_DIR,
            "--bins-per-array-dimension",
            str(DEMO_BINS),
        ]
    )

    assert exit_code == 0
    captured = capsys.readouterr()
    output_path = captured.out.strip()
    assert captured.out == output_path + "\n"
    assert os.path.isfile(output_path)
    assert "Predictions made" in captured.err

    with open(output_path) as f:
        result = json.load(f)
    # Plain bools (no [true]/[false] 1-tuple artifact) - the contract the
    # now-removable C#-side regex hack (Form1.cs:1522-1523) used to work
    # around.
    assert all(isinstance(record["isActive"], bool) for record in result)


def test_main_with_missing_model_dir_exits_1_with_clean_error(tmp_path, capsys):
    json_path = build_demo_json(str(tmp_path / "demo.json"))

    exit_code = main([json_path, str(tmp_path), "--model-dir", str(tmp_path / "no_such_model_dir")])

    assert exit_code == 1
    captured = capsys.readouterr()
    assert captured.err.startswith("Error: ")


def test_main_model_dir_flag_overrides_default(tmp_path, capsys):
    # Confirms --model-dir is wired through, independent of CWD - the .NET
    # call layer must not depend on DEFAULT_MODEL_DIR's CWD-relative default.
    json_path = build_demo_json(str(tmp_path / "demo.json"))
    model_dir = build_synthetic_model_dir(str(tmp_path / "models"))

    exit_code = main(
        [json_path, str(tmp_path), "--model-dir", model_dir, "--bins-per-array-dimension", str(DEMO_BINS)]
    )

    assert exit_code == 0
    captured = capsys.readouterr()
    assert os.path.isfile(captured.out.strip())
