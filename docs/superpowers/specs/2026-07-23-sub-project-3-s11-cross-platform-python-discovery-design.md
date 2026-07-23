# S11 — Cross-platform python/env discovery (design)

## Context

`docs/superpowers/SESSIONS.md`'s S11 was originally scoped as three pieces:
secondary windows (Help/Shortcuts), settings persistence, and cross-platform
python/env path discovery. Per user decision this session, S11 is split into
three: **S11 = discovery only** (this spec), **S11b = settings**, **S11c =
Help/Shortcuts**.

`dotnet/BackendInterop/BackendEnvironment.cs` currently hardcodes a single
Linux/Mac conda path shape for finding the `csp_modern` python env:

```csharp
var candidate = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "miniforge3", "envs", "csp_modern", "bin", "python");
return File.Exists(candidate) ? candidate : null;
```

This breaks on Windows (`python.exe`, no `bin/` subfolder) and only ever
checks `miniforge3`, not `miniconda3`/`anaconda3`. `BackendInterop.Tests/RepoPaths.cs`
independently duplicates a hardcoded python path with a comment flagging
that S11 would replace it.

## Scope

In scope:
- Multi-OS candidate path generation for the named `csp_modern` conda env,
  covering `miniforge3`, `miniconda3`, `anaconda3` under the user's home
  directory, with correct per-OS path shape (Windows `python.exe` directly
  under the env dir; Linux/macOS `bin/python`).
- Refactor `BackendEnvironment.PythonExecutable` to use this candidate list
  and return the first path that exists on disk, else `null` (unchanged
  contract — callers already null-check, see `MainViewModel.cs:259-264`).
- Remove the duplicate hardcoded python path in `BackendInterop.Tests/RepoPaths.cs`,
  replacing it with a call to `BackendEnvironment.PythonExecutable`.
- Unit tests that verify exact candidate path lists for Windows and
  Linux/macOS inputs, runnable on this Linux dev box without needing to
  actually run on those OSes.

Out of scope (deferred):
- `RepoRoot`/`ModelDir` resolution for packaged/installed deployments (still
  requires a `.git` directory next to `backend/`, fine for dev checkouts).
  Revisited in S12 (cross-platform smoke test) or S13 (packaging), once an
  actual installer/package layout exists to design against.
- PATH-based fallback search (bare `python`/`python3` on PATH, or querying
  `conda env list`). Named-env path probing only.
- Any UI for overriding the discovered path (that's S11b's settings work).

## Design

### `CondaPythonPaths` (new, pure, testable)

A static class (new file `dotnet/BackendInterop/CondaPythonPaths.cs`) with:

```csharp
public static IReadOnlyList<string> BuildCandidates(
    OSPlatform platform, string homeDir, string envName)
```

Returns paths for `miniforge3`, `miniconda3`, `anaconda3` (in that order) under
`homeDir`, using **manual string joining with the target platform's separator**
— not `Path.Combine`/`Path.DirectorySeparatorChar`, which always reflect the
*host* OS regardless of the `platform` parameter, and would silently produce
wrong-separator paths when a test simulates Windows while running on Linux.

- `OSPlatform.Windows` → `{homeDir}\{distro}\envs\{envName}\python.exe`
- anything else (Linux, OSX) → `{homeDir}/{distro}/envs/{envName}/bin/python`

### `BackendEnvironment.PythonExecutable` (modified)

```csharp
public static string? PythonExecutable
{
    get
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX
            : OSPlatform.Linux;
        return CondaPythonPaths.BuildCandidates(platform, home, "csp_modern")
            .FirstOrDefault(File.Exists);
    }
}
```

Env name `csp_modern` stays a literal here (not parameterized further —
YAGNI, nothing else in the codebase needs a different env name).

### `BackendInterop.Tests/RepoPaths.cs`

Its independent hardcoded python-path logic is deleted; call sites use
`BackendEnvironment.PythonExecutable` directly.

## Testing

- New `CondaPythonPathsTests.cs`: asserts exact candidate list (paths,
  order, separators) for `OSPlatform.Windows` and `OSPlatform.Linux` (and
  `OSX`, same shape as Linux) given a fixed fake home dir — no dependency
  on the actual host OS.
- Existing `BackendCliRunnerIntegrationTests.cs` (shells out to the real
  `csp_modern` env on this box) stays the regression safety net for the
  file-existence-probing half, unchanged.
- Full suite: `dotnet test CspAnalyzer.sln` must stay green, including the
  real-environment integration test (proves the refactor didn't break
  discovery on this actual dev box).

## Non-goals recap

No settings UI, no Help/Shortcuts windows, no packaged-app path resolution,
no PATH search — all explicitly deferred per the scoping discussion above.
