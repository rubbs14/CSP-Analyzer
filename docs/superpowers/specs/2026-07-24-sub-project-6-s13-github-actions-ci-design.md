# S13 — GitHub Actions CI (design)

**Sub-project 6 — CI / packaging.** First session of the sub-project.

## Goal

Automated build+test on every push and pull request, across the three OSes
the app is meant to run on (Linux/Windows/macOS — cross-platform is the whole
point of the Avalonia rewrite, per the original modernization scope).
Currently there is no CI at all; regressions are only caught by whoever
happens to run tests locally.

## Scope

One workflow file: `.github/workflows/ci.yml`. Two jobs, `python-tests` and
`dotnet-tests`, each matrixed over `os: [ubuntu-latest, windows-latest,
macos-latest]` with `fail-fast: false` (a failure on one OS must not cancel
the runs on the others — the whole point of the matrix is seeing all three
results).

Triggers: `push` (any branch) and `pull_request`.

Explicitly out of scope (deferred to S14): publishing/packaging build
artifacts, installers, or resolving `BackendEnvironment.FindRepoRoot()`'s
`.git`-folder assumption for a packaged (non-repo) install.

## `python-tests` job

Steps, identical across all 3 OSes:

1. `actions/checkout@v4`
2. `actions/setup-python@v5`, `python-version: '3.12'` (matches the local
   `csp_modern` dev env's version), `cache: 'pip'` keyed on
   `backend/requirements.txt` + `backend/requirements-dev.txt` hashes.
3. `pip install -r backend/requirements.txt -r backend/requirements-dev.txt`
   — plain pip, no conda. numpy/scipy/scikit-image/scikit-learn all publish
   prebuilt wheels for `win_amd64`/`macosx`/`manylinux`, so this one command
   is identical on every runner OS. The local `csp_modern` conda env is a
   dev-machine convenience only; CI is unaware it exists and doesn't need
   parity with it.
4. `pytest backend/tests`, run from repo root (checkout's default CWD) so
   the `backend` package is importable — same invocation as local dev.

## `dotnet-tests` job

Steps, identical across all 3 OSes:

1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4`, `dotnet-version: '8.0.x'`
3. `dotnet restore dotnet/CspAnalyzer.sln`
4. `dotnet build dotnet/CspAnalyzer.sln --configuration Release --no-restore`
5. `dotnet test dotnet/CspAnalyzer.sln --configuration Release --no-build`

Notes on why this needs no OS-specific branching:

- `CspAnalyzer.Desktop.Tests`' Avalonia headless suite
  (`AvaloniaHeadlessPlatformOptions`) needs no display server — runs as-is
  on a headless CI runner on all 3 OSes, same as it already does locally
  without `xdotool`/a real X session.
- `BackendInterop.Tests`' integration test (the one that shells to a real
  `csp_modern` conda python) already self-skips when that interpreter path
  isn't found — true on every CI runner, since none of them have that conda
  env. No CI-specific skip logic needed; this is the same behavior the test
  already has on a dev machine with the env deactivated.
- The managed/IL nature of a normal (non-native-AOT) .NET build means
  compiling on Linux doesn't need to "cross-compile for Windows" in any
  special sense — but this design still runs the matrix on real
  windows-latest/macos-latest hosted runners rather than relying on that,
  since the goal is proving the tests *actually pass* on those OSes (e.g.
  S11's Windows-shaped conda path discovery, exercised for real here instead
  of only via the `OSPlatform`-parameterized unit tests), not just that the
  IL is portable.

## Caching

Pip cache via `setup-python`'s built-in `cache: 'pip'` input — no extra
config. NuGet restore caching is deliberately left out of this first version
(`dotnet restore` without a cache is simple and correct; add
`actions/cache` keyed on `packages.lock.json` later only if restore time
becomes a real annoyance — no lock files exist in this repo yet, so there's
nothing to key a cache on today).

## Testing / verification

- Push the workflow file to a branch, open a PR (or push to `master`
  directly per repo convention) and confirm all 6 jobs (2 jobs × 3 OS) go
  green in the Actions tab.
- If any OS-specific failure surfaces (e.g. a path-separator or line-ending
  issue not caught by S11's simulated-`OSPlatform` unit tests), fix it as
  part of this session rather than deferring — that's precisely the kind of
  gap a real multi-OS runner is meant to catch that local-only testing
  can't.

## Out of scope / deferred

- S14: packaging/installers, `FindRepoRoot()`'s packaged-install story,
  bundled-vs-user-conda python distribution decision.
- NuGet restore caching (see above).
- Concurrency cancellation of superseded runs on the same PR/branch — not
  needed for a first green baseline.
