# S13 GitHub Actions CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add automated build+test CI (`.github/workflows/ci.yml`) that runs the python test suite and the .NET test suite on every push and pull request, across ubuntu-latest/windows-latest/macos-latest.

**Architecture:** One GitHub Actions workflow file, two independent jobs (`python-tests`, `dotnet-tests`), each matrixed over the 3 OSes with `fail-fast: false`. No conda anywhere in CI — plain `pip install` for python deps (prebuilt wheels exist for all 3 OSes); plain `dotnet` CLI for the .NET side (managed IL, no native toolchain needed).

**Tech Stack:** GitHub Actions (`actions/checkout@v4`, `actions/setup-python@v5`, `actions/setup-dotnet@v4`), pytest, `dotnet test` (xunit under the hood), gh CLI for verification.

## Global Constraints

- Python version in CI: `3.12` (matches local `csp_modern` dev env — spec section "python-tests job").
- .NET SDK version in CI: `8.0.x` (matches installed local SDK `8.0.129` — spec section "dotnet-tests job").
- Matrix OS list for **both** jobs: `[ubuntu-latest, windows-latest, macos-latest]`, `fail-fast: false` (spec "Scope").
- Triggers: `push` (any branch) and `pull_request` (spec "Scope").
- No conda in CI, no OS-specific branching in either job's steps (spec "python-tests job" / "dotnet-tests job" notes).
- Out of scope this plan: packaging/installers, NuGet restore caching, concurrency-cancellation config (spec "Out of scope / deferred").
- Push strategy for verification: push `master` directly to `origin/master` (fast-forward, confirmed with user — not a feature branch/PR).

---

### Task 1: `python-tests` job

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `backend/requirements.txt`, `backend/requirements-dev.txt` (existing files, already used identically by local `pytest backend/tests` runs per `backend/README.md`).
- Produces: a `python-tests` job in `.github/workflows/ci.yml` that Task 2 will append `dotnet-tests` alongside (same file, sibling top-level key under `jobs:`).

- [ ] **Step 1: Write `.github/workflows/ci.yml` with the `python-tests` job**

```yaml
name: CI

on:
  push:
  pull_request:

jobs:
  python-tests:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-python@v5
        with:
          python-version: '3.12'
          cache: 'pip'
          cache-dependency-path: |
            backend/requirements.txt
            backend/requirements-dev.txt

      - name: Install python dependencies
        run: pip install -r backend/requirements.txt -r backend/requirements-dev.txt

      - name: Run pytest
        run: pytest backend/tests
```

- [ ] **Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
Add python-tests CI job (S13)

pytest backend/tests across ubuntu/windows/macos via plain pip, no conda.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: `dotnet-tests` job

**Files:**
- Modify: `.github/workflows/ci.yml` (append sibling job under `jobs:`, from Task 1)

**Interfaces:**
- Consumes: `dotnet/CspAnalyzer.sln` (existing solution — `BackendInterop`, `BackendInterop.Tests`, `CspAnalyzer.Desktop`, `CspAnalyzer.Desktop.Tests` projects, per S7).
- Produces: a `dotnet-tests` job, sibling to `python-tests` in the same workflow file. Nothing downstream in this plan consumes it further — Task 3 verifies both jobs together.

- [ ] **Step 1: Append the `dotnet-tests` job to `.github/workflows/ci.yml`**

Add this as a new top-level entry under `jobs:`, alongside `python-tests` (resulting file has both jobs at the same indentation level):

```yaml
  dotnet-tests:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore dotnet/CspAnalyzer.sln

      - name: Build
        run: dotnet build dotnet/CspAnalyzer.sln --configuration Release --no-restore

      - name: Test
        run: dotnet test dotnet/CspAnalyzer.sln --configuration Release --no-build
```

- [ ] **Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Confirm both jobs are present as sibling keys**

Run: `python3 -c "import yaml; d = yaml.safe_load(open('.github/workflows/ci.yml')); print(sorted(d['jobs'].keys()))"`
Expected: `['dotnet-tests', 'python-tests']`

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "$(cat <<'EOF'
Add dotnet-tests CI job (S13)

dotnet test CspAnalyzer.sln across ubuntu/windows/macos. Desktop.Tests'
Avalonia headless suite needs no display; BackendInterop.Tests'
csp_modern-conda integration test self-skips when that interpreter
is absent, same as it already does on a dev machine with the env
deactivated.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Push and verify on GitHub

**Files:** none (verification-only task; no new file changes).

**Interfaces:**
- Consumes: the committed `.github/workflows/ci.yml` from Tasks 1–2; `origin` remote (`git@github.com:rubbs14/CSP-Analyzer.git`, confirmed reachable and already authenticated via `gh auth status`).
- Produces: a green (or fixed-until-green) CI run on GitHub — the deliverable this whole plan exists for.

- [ ] **Step 1: Push master to origin**

Run: `git push origin master`
Expected: fast-forward push succeeds (origin/master was a strict ancestor of local master at plan-writing time — re-check with `git log --oneline master..origin/master` immediately before pushing in case anything changed; if that command shows any commits, stop and re-sync before pushing rather than force-pushing).

- [ ] **Step 2: Find the triggered workflow run**

Run: `gh run list --workflow=ci.yml --limit 1`
Expected: one row, `status` = `in_progress` or `queued`, tied to the just-pushed commit SHA.

- [ ] **Step 3: Watch the run to completion**

Run: `gh run watch <run-id> --exit-status` (substitute the run ID from Step 2)
Expected: command exits `0` once all 6 jobs (`python-tests` × 3 OS, `dotnet-tests` × 3 OS) complete. If it exits non-zero, proceed to Step 4.

- [ ] **Step 4: If any job failed, inspect and fix**

Run: `gh run view <run-id> --log-failed`
Read the failure output. Common categories to expect, per the spec's own risk notes:
- A python dependency failing to build/install on one OS (unlikely — all 4 packages ship prebuilt wheels for win_amd64/macosx/manylinux, but check the exact pip error if this happens).
- A `dotnet test` failure specific to one OS (e.g. a path-separator assumption the S11 `OSPlatform`-simulated unit tests didn't catch for real, since those tests build target-OS path strings without ever running on that OS).
Fix the root cause in the relevant source file (not by skipping/disabling the failing OS from the matrix), commit, push, and repeat Steps 2–4 until Step 3 exits 0.

- [ ] **Step 5: Update SESSIONS.md**

In `docs/superpowers/SESSIONS.md`, change the S13 checkbox from `- [ ]` to `- [x]` and append a short completion summary (matching the style of every prior session's entry — what was built, what verification confirmed, any gotchas hit in Step 4). Commit this alongside no other unrelated changes:

```bash
git add docs/superpowers/SESSIONS.md
git commit -m "$(cat <<'EOF'
S13: mark complete in SESSIONS.md with summary

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
git push origin master
```

## Final Verification

- [ ] `gh run list --workflow=ci.yml --limit 1` shows the latest run with conclusion `success`.
- [ ] `gh run view <run-id>` lists exactly 6 jobs, all with conclusion `success` (`python-tests` and `dotnet-tests`, each × ubuntu/windows/macos).
- [ ] `docs/superpowers/SESSIONS.md` shows S13 checked off with a completion note.
