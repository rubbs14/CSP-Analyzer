# S11 Cross-Platform Python Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `BackendEnvironment.PythonExecutable`'s single hardcoded Linux/Mac conda path with cross-platform candidate-path probing for the `csp_modern` env (Windows/Linux/macOS, across `miniforge3`/`miniconda3`/`anaconda3`), and remove the duplicate hardcoded path in the test project.

**Architecture:** A new pure, static `CondaPythonPaths.BuildCandidates(OSPlatform, string homeDir, string envName)` function builds the ordered candidate path list using manual separator-aware string joining (never `Path.Combine`, which always reflects the host OS regardless of the `platform` argument). `BackendEnvironment.PythonExecutable` detects the real host platform via `RuntimeInformation`, gets candidates from this function, and returns the first one that exists on disk (unchanged `string?` contract). `BackendInterop.Tests/RepoPaths.CspModernPythonExecutable` becomes a thin delegate to `BackendEnvironment.PythonExecutable` instead of its own hardcoded copy.

**Tech Stack:** .NET 8, C# (`CspAnalyzer.BackendInterop` class library), xunit (`CspAnalyzer.BackendInterop.Tests`).

## Global Constraints

- Named conda env is always `csp_modern` (hardcoded literal, not parameterized — no other env name is ever used in this codebase).
- Distro search order: `miniforge3`, `miniconda3`, `anaconda3` (first match wins).
- Windows path shape: `{home}\{distro}\envs\{envName}\python.exe` (no `bin\`).
- Linux/macOS path shape: `{home}/{distro}/envs/{envName}/bin/python`.
- `BackendEnvironment.PythonExecutable`'s public contract (`string?`, null when not found) must not change — `MainViewModel.cs:259-264` already null-checks it.
- Out of scope: `RepoRoot`/`ModelDir` packaged-app resolution, PATH-based search, any settings/override UI (deferred to S12/S13/S11b per the approved spec).
- Full `dotnet test CspAnalyzer.sln` (including the real-environment integration tests in `BackendCliRunnerIntegrationTests.cs`) must stay green throughout — this is the regression safety net for the actual-file-probing half of discovery on this real dev box.

---

### Task 1: `CondaPythonPaths` pure candidate-path builder

**Files:**
- Create: `dotnet/BackendInterop/CondaPythonPaths.cs`
- Test: `dotnet/BackendInterop.Tests/CondaPythonPathsTests.cs`

**Interfaces:**
- Produces: `public static class CondaPythonPaths { public static IReadOnlyList<string> BuildCandidates(OSPlatform platform, string homeDir, string envName) }` — used by Task 2.

- [ ] **Step 1: Write the failing tests**

Create `dotnet/BackendInterop.Tests/CondaPythonPathsTests.cs`:

```csharp
using System.Runtime.InteropServices;
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class CondaPythonPathsTests
{
    [Fact]
    public void BuildCandidates_windows_returns_python_exe_paths_under_each_distro()
    {
        var candidates = CondaPythonPaths.BuildCandidates(OSPlatform.Windows, @"C:\Users\bob", "csp_modern");

        Assert.Equal(new[]
        {
            @"C:\Users\bob\miniforge3\envs\csp_modern\python.exe",
            @"C:\Users\bob\miniconda3\envs\csp_modern\python.exe",
            @"C:\Users\bob\anaconda3\envs\csp_modern\python.exe",
        }, candidates);
    }

    [Fact]
    public void BuildCandidates_linux_returns_bin_python_paths_under_each_distro()
    {
        var candidates = CondaPythonPaths.BuildCandidates(OSPlatform.Linux, "/home/bob", "csp_modern");

        Assert.Equal(new[]
        {
            "/home/bob/miniforge3/envs/csp_modern/bin/python",
            "/home/bob/miniconda3/envs/csp_modern/bin/python",
            "/home/bob/anaconda3/envs/csp_modern/bin/python",
        }, candidates);
    }

    [Fact]
    public void BuildCandidates_osx_uses_same_shape_as_linux()
    {
        var candidates = CondaPythonPaths.BuildCandidates(OSPlatform.OSX, "/Users/bob", "csp_modern");

        Assert.Equal(new[]
        {
            "/Users/bob/miniforge3/envs/csp_modern/bin/python",
            "/Users/bob/miniconda3/envs/csp_modern/bin/python",
            "/Users/bob/anaconda3/envs/csp_modern/bin/python",
        }, candidates);
    }

    [Fact]
    public void BuildCandidates_returns_three_candidates_in_distro_priority_order()
    {
        var candidates = CondaPythonPaths.BuildCandidates(OSPlatform.Linux, "/home/bob", "csp_modern");

        Assert.Equal(3, candidates.Count);
        Assert.Contains("miniforge3", candidates[0]);
        Assert.Contains("miniconda3", candidates[1]);
        Assert.Contains("anaconda3", candidates[2]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter CondaPythonPathsTests`
Expected: build error — `CondaPythonPaths` does not exist yet.

- [ ] **Step 3: Write the implementation**

Create `dotnet/BackendInterop/CondaPythonPaths.cs`:

```csharp
using System.Runtime.InteropServices;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Pure candidate-path builder for locating a named conda env's python
/// executable across OSes. Kept separate from BackendEnvironment so the
/// path-shape logic is unit-testable for Windows/macOS without actually
/// running on those OSes: Path.Combine/Path.DirectorySeparatorChar always
/// reflect the *host* OS, not a simulated target platform, so paths here
/// are joined manually with the target platform's separator instead.
/// </summary>
public static class CondaPythonPaths
{
    private static readonly string[] DistrosInPriorityOrder = { "miniforge3", "miniconda3", "anaconda3" };

    public static IReadOnlyList<string> BuildCandidates(OSPlatform platform, string homeDir, string envName)
    {
        var isWindows = platform == OSPlatform.Windows;
        var separator = isWindows ? '\\' : '/';

        var candidates = new List<string>(DistrosInPriorityOrder.Length);
        foreach (var distro in DistrosInPriorityOrder)
        {
            var segments = isWindows
                ? new[] { homeDir, distro, "envs", envName, "python.exe" }
                : new[] { homeDir, distro, "envs", envName, "bin", "python" };

            candidates.Add(string.Join(separator, segments));
        }

        return candidates;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter CondaPythonPathsTests`
Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add dotnet/BackendInterop/CondaPythonPaths.cs dotnet/BackendInterop.Tests/CondaPythonPathsTests.cs
git commit -m "S11: add CondaPythonPaths cross-platform candidate-path builder"
```

---

### Task 2: Wire `BackendEnvironment.PythonExecutable` to `CondaPythonPaths`

**Files:**
- Modify: `dotnet/BackendInterop/BackendEnvironment.cs`

**Interfaces:**
- Consumes: `CondaPythonPaths.BuildCandidates(OSPlatform, string, string)` from Task 1.
- Produces: `BackendEnvironment.PythonExecutable` (`string?`, unchanged public signature) — consumed by `MainViewModel.cs:259-264` (unmodified) and Task 3's `RepoPaths` delegate.

- [ ] **Step 1: Replace the hardcoded single-path logic**

Edit `dotnet/BackendInterop/BackendEnvironment.cs` — replace the whole file with:

```csharp
using System.Runtime.InteropServices;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Minimal runtime discovery so CspAnalyzer.Desktop can actually invoke
/// BackendCliRunner without every call site hardcoding paths (S9).
/// PythonExecutable probes cross-platform conda env locations via
/// CondaPythonPaths (S11). RepoRoot/ModelDir still require a .git
/// directory next to backend/ - fine for dev checkouts, packaged-app
/// resolution is deferred to S12/S13.
/// </summary>
public static class BackendEnvironment
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ModelDir => Path.Combine(RepoRoot, "backend", "model_artifacts");

    /// <summary>
    /// Path to the csp_modern conda env's python, or null if that exact
    /// env isn't present on the current machine under any known conda
    /// distro (miniforge3/miniconda3/anaconda3).
    /// </summary>
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

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate repo root (backend/ + .git/) above {AppContext.BaseDirectory}");
    }
}
```

- [ ] **Step 2: Run the existing BackendEnvironment tests to verify the contract still holds**

Run: `dotnet test dotnet/CspAnalyzer.sln --filter BackendEnvironmentTests`
Expected: 3 passed (`RepoRoot_contains_backend_package_and_git_dir`, `ModelDir_points_at_the_real_checked_in_model_artifacts_dir`, `PythonExecutable_is_null_or_an_existing_file`) — the last one exercises the new candidate-probing path for real on this box and must still resolve the real `csp_modern` env if present.

- [ ] **Step 3: Commit**

```bash
git add dotnet/BackendInterop/BackendEnvironment.cs
git commit -m "S11: BackendEnvironment.PythonExecutable probes cross-platform conda paths"
```

---

### Task 3: Collapse `RepoPaths`'s duplicate + refresh stale doc comments

**Files:**
- Modify: `dotnet/BackendInterop.Tests/RepoPaths.cs`
- Modify: `dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs:5-11,36` (class + inline doc comments referencing S11 as future work)

**Interfaces:**
- Consumes: `BackendEnvironment.PythonExecutable` from Task 2.
- Produces: `RepoPaths.CspModernPythonExecutable` (`string?`, same name/signature as before — `BackendCliRunnerIntegrationTests.cs`'s four call sites are unmodified).

- [ ] **Step 1: Replace `RepoPaths.CspModernPythonExecutable`'s body with a delegate**

Edit `dotnet/BackendInterop.Tests/RepoPaths.cs` in full:

```csharp
namespace CspAnalyzer.BackendInterop.Tests;

/// <summary>
/// Finds repo-relative paths (real model_artifacts/) without hardcoding a
/// fixed number of ".." segments, since the test assembly's output path
/// depth varies by build configuration.
/// </summary>
internal static class RepoPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string RealModelArtifactsDir => Path.Combine(RepoRoot, "backend", "model_artifacts");

    /// <summary>
    /// Delegates to BackendEnvironment.PythonExecutable (S11) rather than
    /// duplicating its own path-discovery logic. Null if the csp_modern
    /// conda env isn't present on the current machine (the integration
    /// test skips itself in that case).
    /// </summary>
    public static string? CspModernPythonExecutable => BackendEnvironment.PythonExecutable;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate repo root (backend/ + .git/) above {AppContext.BaseDirectory}");
    }
}
```

- [ ] **Step 2: Refresh stale doc comments in `BackendEnvironmentTests.cs`**

In `dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs`, replace the class doc comment (lines 5-11):

```csharp
/// <summary>
/// BackendEnvironment is the app-runtime counterpart of this test project's
/// own RepoPaths helper - same repo-root discovery strategy (walk up from
/// the running assembly looking for backend/ + .git/), but living in
/// BackendInterop so CspAnalyzer.Desktop can use it too. PythonExecutable
/// probes cross-platform conda env locations (S11); RepoPaths now delegates
/// to it instead of duplicating the logic.
/// </summary>
```

And the inline comment inside `PythonExecutable_is_null_or_an_existing_file` (lines 35-37):

```csharp
        // Can't assert presence (machine-dependent) - only that the
        // contract (null when absent, a real existing path when present)
        // holds. Candidate-path shape itself is covered by
        // CondaPythonPathsTests, which doesn't depend on the real machine.
```

- [ ] **Step 3: Run the full solution test suite**

Run: `dotnet build dotnet/CspAnalyzer.sln && dotnet test dotnet/CspAnalyzer.sln`
Expected: build succeeds with 0 errors/warnings from these changes; all tests pass, including the real-environment `BackendCliRunnerIntegrationTests` (proves `RepoPaths.CspModernPythonExecutable`'s delegation still resolves the real `csp_modern` env on this box) and the new `CondaPythonPathsTests`.

- [ ] **Step 4: Commit**

```bash
git add dotnet/BackendInterop.Tests/RepoPaths.cs dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs
git commit -m "S11: RepoPaths delegates to BackendEnvironment.PythonExecutable, drop duplicate"
```

---

## Final Verification

- [ ] Run `dotnet test dotnet/CspAnalyzer.sln` one more time from a clean state and confirm full green (all projects: `BackendInterop.Tests`, `CspAnalyzer.Desktop.Tests`).
- [ ] Update `docs/superpowers/SESSIONS.md`: check off `S11` (already split from the original bundled item in the prior session).
