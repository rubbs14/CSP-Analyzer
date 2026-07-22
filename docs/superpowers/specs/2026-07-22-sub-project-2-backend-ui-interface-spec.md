# Sub-project 2 — Backend ↔ UI interface (S6)

Status: **done** (2026-07-22).

## Problem

The Avalonia UI (sub-project 3, S7+) needs a stable way to invoke the python
backend and read its result. Today's WinForms code (`CSPv2/Form1.cs`) does
this badly and is being deleted wholesale with WinForms itself, not fixed in
place:

- `Form1.cs:1440-1444` builds a `cmd.exe /c` command line by naive string
  concatenation (conda activate && python && script && json-arg-path), with
  one dead escaping attempt (`cmdlike`, computed but never used) - command-
  injection-prone since the json path embeds a dataset name.
- `Form1.cs:1451-1462` (`run_cmd`) launches it via `cmd.exe`, calls
  `WaitForExit()`, and never reads `ExitCode`, stdout, or stderr. The only
  failure signal is whatever exception falls out of parsing
  `processed_spectra.json` afterward (`Form1.cs:1517-1560`), caught by one
  generic `catch (Exception conda)` that can't distinguish "python not
  found" from "bad input" from "model failed to load."
- `Form1.cs:1522-1523` regex-rewrites `[true]`/`[false]` to `true`/`false` -
  a workaround for a python tuple-formatting bug that S1 already fixed on
  the backend side (`backend/io.py:60`, `bool(...)` not a 1-tuple). The
  workaround is dead weight but harmless since it only replaces text that no
  longer appears.

None of this is touched here: it's WinForms-only code that S7 deletes along
with the rest of `Form1.cs`. Editing it now would be wasted effort. What S6
actually delivers is the *contract* the new Avalonia call layer follows
instead, plus a minimal .NET stub proving that contract is usable, so S9
("Run flow: invoke python backend via the S6 contract") has something
concrete to build on.

## The contract

**Invocation** (`backend/__main__.py`):

```
python -m backend <json_in> [out_dir] [--model-dir DIR] [--bins-per-array-dimension N]
```

- `json_in` (required, positional) - path to the peaklist JSON.
- `out_dir` (optional, positional) - defaults to `json_in`'s directory.
  `processed_spectra.json` is written there.
- `--model-dir` (optional flag, default `model_artifacts` relative to CWD) -
  **the .NET caller must always pass this as an absolute path.** The
  process's CWD is not something a GUI app run from an arbitrary install
  directory should be relied on to control; `--model-dir` existed as a
  library-only `run()` kwarg before S6 (added in S4) but wasn't reachable
  from the CLI, so the CLI contract silently depended on CWD. Closed here by
  exposing the flag and documenting the caller's obligation instead of
  leaving it implicit.
- `--bins-per-array-dimension` (optional flag, default `500`) - exposed for
  the same reason: it was already a `run()` kwarg (the production default is
  500; the demo/test fixtures use 128 - see the S3 "golden-baseline gotcha"
  in `[[csp-analyzer-upgrade]]` memory for why a silent mismatch here is
  dangerous). A caller that overrides one should be able to override the
  other without editing python.

**Exit codes** (all three now hit paths that used to be indistinguishable):

| Code | Meaning | Where |
|------|---------|-------|
| `0`  | Success. | after `run()` returns |
| `1`  | `run()` raised - bad input, missing/corrupt model dir, or any other runtime failure. **Every** exception type is caught now (previously only `ValueError`/`RuntimeError`/`FileNotFoundError` - anything else, e.g. a `KeyError` from malformed JSON, used to leak a raw traceback with no `Error:` prefix and no way for a caller to tell "it printed a traceback" from "it printed nothing"). | `except Exception as exc` in `main()` |
| `2`  | Usage error - missing/malformed argv. | `argparse`'s own exit code, propagated as-is from the `SystemExit` it raises |

`--help` is also handled correctly (exits `0`, help text to stdout) since
argparse's own `SystemExit.code` is propagated unchanged rather than forced
to a fixed value.

**stdout / stderr split** (the part that actually matters for a calling
process, and wasn't decided before S6):

- **stdout, on success, is *exactly one line*: the absolute output path.**
  Nothing else goes to stdout. This replaces the old prose message
  (`"Predictions made, saved to file at {path}"`, still printed, but now to
  **stderr**) as the machine-readable success signal - a caller reads
  stdout, trims it, and that's the path to `processed_spectra.json`. No
  string-matching or parsing needed.
- **stderr, on failure, is a single line**: `Error: {message}`. No
  traceback (guaranteed by the broad `except Exception`).
- **stderr, on success**, additionally carries the human-readable status
  line - informational only, not part of the contract, safe to log or
  ignore.

**Caller obligations** (what the .NET call layer does, and what S9 must not
skip):

1. Launch with `UseShellExecute = false`, `RedirectStandardOutput = true`,
   `RedirectStandardError = true`, and build arguments via
   `ArgumentList` (never a concatenated string) - the injection-prone
   pattern this replaces is exactly `Form1.cs`'s string-built `cmd.exe /c`
   line.
2. Wait for exit, **check `ExitCode == 0` before trusting stdout** as a
   path. On non-zero exit, surface the captured stderr text to the user
   instead of attempting to read `processed_spectra.json`.
3. Only on `ExitCode == 0`, trim stdout and treat it as the output path.
4. Always pass `--model-dir` as an absolute path resolved by the .NET side
   (e.g. relative to the app's own install/bundle directory), never rely on
   the python process's CWD.
5. Set the process's `WorkingDirectory` to the directory containing the
   `backend/` package (the repo root, or wherever it's deployed to) - the
   backend isn't pip-installed, so `python -m backend` only resolves if CWD
   (or `PYTHONPATH`) puts it on the import path. `BackendCliRunner.Run`
   below takes this as a required `workingDirectory` parameter for exactly
   this reason - found by the integration test failing with `No module
   named backend` before it was added.

**Output JSON schema** (`processed_spectra.json`, written by
`backend/io.py:json_constructor`, unchanged by S6): a JSON array, one object
per experiment spectrum:

```json
[{"EXP_NUMBER": 1, "isActive": true, "activePseudoprobability": 0.87}, ...]
```

`isActive` is a plain JSON bool (the S1 tuple-bug fix) - a caller does not
need `Form1.cs:1522-1523`'s `[true]`/`[false]` regex rewrite. The `.NET`
POCO in `BackendInterop` (below) deserializes this directly via
`System.Text.Json` as proof.

## Explicitly out of scope for S6

- **Python-executable / conda-env discovery and path resolution across
  Linux/Windows/Mac.** That's S11 ("Secondary windows, settings,
  python/env path handling done cross-platform"). The `BackendInterop` stub
  below takes the python executable path as an explicit parameter; it does
  no searching.
- **Editing `Form1.cs`.** Dead-end WinForms code, deleted wholesale in S7+,
  not fixed in place - see "Problem" above.
- **Alternative to shell-out** (an in-process/local API was named as an
  option in the original design doc's roadmap but never explored) - shelling
  out to a CLI process remains the chosen mechanism; it keeps the python
  side a plain, independently-testable CLI (already exercised end-to-end by
  `backend/tests/test_cli.py`) and avoids embedding a python interpreter or
  standing up an RPC server for a single-shot, one-request-per-run workload.

## Minimal .NET call-layer stub

`dotnet/BackendInterop/` (new, SDK-style, `net8.0`, standalone from the
legacy `CSPv2.sln` - S7 folds it into the Avalonia solution once that
exists):

- `BackendCliRunner.Run(pythonExecutable, jsonIn, outDir, modelDir,
  workingDirectory, binsPerArrayDimension)` - builds the argument list per the contract above,
  launches the process per the caller obligations above, and returns a
  `BackendRunResult` (`ExitCode`, `StdOut`, `StdErr`, `IsSuccess`,
  `OutputPath` - trimmed stdout only when `IsSuccess`).
- `SpectrumResult` (`ExpNumber`, `IsActive`, `ActivePseudoprobability`) +
  `SpectrumResult.ParseArray(json)` - `System.Text.Json` deserialization of
  the output schema above, with explicit `JsonPropertyName` attributes
  matching the python-side field names.

`dotnet/BackendInterop.Tests/` (xunit):

- Unit test: `SpectrumResult.ParseArray` against a literal plain-bool JSON
  string - confirms no regex workaround is needed on the .NET side.
- Integration test: actually shells out to the real `csp_modern` conda env's
  python (`~/miniforge3/envs/csp_modern/bin/python`, skipped if that path
  doesn't exist - this machine-specific path is exactly the thing S11 will
  replace with real discovery) against a committed demo fixture JSON and the
  repo's real `backend/model_artifacts/`, with `--bins-per-array-dimension
  128` (matching the fixture, per the golden-baseline gotcha above) -
  asserts `ExitCode == 0`, a well-formed `OutputPath`, and that
  `SpectrumResult.ParseArray` parses the real file.

## Consequences / follow-ups noted, not actioned here

- `Form1.cs:1522-1523`'s regex hack and the `Form1.cs:1440-1462` shell-out
  path stay in place until S7 deletes `Form1.cs` outright - confirmed
  harmless in the meantime (the regex only matches text the backend no
  longer emits).
- `backend/__main__.py`'s `run()` library signature is unchanged; only
  `main()`'s argv parsing and exit/stdout/stderr behavior changed. Existing
  callers of `run()` directly (all current tests) are unaffected.
