# S14: Cross-Platform Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce self-contained, downloadable CSP-Analyzer builds for Linux/Windows/Mac (PyInstaller-frozen python backend + `dotnet publish --self-contained`), and bring `README.md` up to date with install instructions and a synopsis of the paper this tool implements.

**Architecture:** A PyInstaller freeze turns `backend/` into a standalone `csp-backend` executable per OS (built natively per-OS in CI, no cross-compiling). A `dotnet publish -r <rid> --self-contained` build produces the Avalonia app. A packaging script combines both plus `model_artifacts/` into one folder (all siblings under the app's own directory) and zips it. `BackendEnvironment`/`BackendCliRunner` gain a "packaged layout" detection path so the app finds its bundled backend without needing a `.git` folder nearby.

**Tech Stack:** .NET 8 (`dotnet publish --self-contained`), PyInstaller 6.21.0, PowerShell 7 (`pwsh`, preinstalled on all three GitHub-hosted runner images — this is what makes one packaging script work across `ubuntu-latest`/`windows-latest`/`macos-latest` instead of maintaining separate bash/batch scripts), GitHub Actions (`workflow_dispatch`).

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-24-sub-project-6-s14-cross-platform-packaging-design.md` — read it before starting, it has the full rationale for every decision below.
- Packaged layout: `model_artifacts/` and the frozen backend dist (`csp-backend/csp-backend` or `csp-backend/csp-backend.exe`) live as siblings of the app executable, both under `AppContext.BaseDirectory`.
- No real installers (MSI/dmg/AppImage/deb), no code signing, no arm64/osx-arm64, no auto-update — self-contained zip artifacts only.
- CI packaging job is `workflow_dispatch`-only — never runs on push/PR.
- x64 only: `win-x64`, `linux-x64`, `osx-x64`.
- Never break the existing dev-checkout flow (`.git` walk-up + `csp_modern` conda env) — packaged-mode is an additional path, not a replacement.

---

### Task 1: `BackendExecutable` type + `BackendCliRunner` API change

**Files:**
- Modify: `dotnet/BackendInterop/BackendCliRunner.cs`
- Modify: `dotnet/BackendInterop.Tests/BackendCliRunnerIntegrationTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (~line 305-338, the `RunAsync` command method)
- Test (new): `dotnet/BackendInterop.Tests/BackendCliRunnerArgumentListTests.cs`

**Interfaces:**
- Produces: `public sealed record BackendExecutable(string FileName, IReadOnlyList<string> LeadingArgs)` — `FileName` is the process to launch, `LeadingArgs` are args prepended before `jsonIn` (e.g. `["-m", "backend"]` for a python interpreter, `[]` for a frozen executable that already *is* the backend entrypoint).
- Produces: `BackendCliRunner.Run(BackendExecutable executable, string jsonIn, string? outDir, string modelDir, string workingDirectory, int? binsPerArrayDimension = null)` and the `RunAsync` sibling — same shape as today but `executable` replaces the old `pythonExecutable: string` first parameter.
- Produces: `public static IReadOnlyList<string> BackendCliRunner.BuildArgumentList(BackendExecutable executable, string jsonIn, string? outDir, string modelDir, int? binsPerArrayDimension)` — pure, no process spawn, used both internally by `BuildStartInfo` and directly by unit tests.

This task does **not** touch `BackendEnvironment` yet — `MainViewModel` gets a minimal inline fix (construct a `BackendExecutable` from the existing `BackendEnvironment.PythonExecutable` + `BackendEnvironment.RepoRoot`) just to keep the solution building. Task 2 replaces that inline construction with the real packaged-aware API.

- [ ] **Step 1: Write the failing unit tests for `BuildArgumentList`**

Create `dotnet/BackendInterop.Tests/BackendCliRunnerArgumentListTests.cs`:

```csharp
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class BackendCliRunnerArgumentListTests
{
    [Fact]
    public void Dev_mode_executable_prepends_leading_args_before_jsonIn()
    {
        var executable = new BackendExecutable("/usr/bin/python", new[] { "-m", "backend" });

        var args = BackendCliRunner.BuildArgumentList(executable, "in.json", "out", "models", 128);

        Assert.Equal(new[] { "-m", "backend", "in.json", "out", "--model-dir", "models", "--bins-per-array-dimension", "128" }, args);
    }

    [Fact]
    public void Packaged_mode_executable_has_no_leading_args()
    {
        var executable = new BackendExecutable("/opt/app/csp-backend", Array.Empty<string>());

        var args = BackendCliRunner.BuildArgumentList(executable, "in.json", null, "models", null);

        Assert.Equal(new[] { "in.json", "--model-dir", "models" }, args);
    }

    [Fact]
    public void OutDir_omitted_when_null()
    {
        var executable = new BackendExecutable("python", new[] { "-m", "backend" });

        var args = BackendCliRunner.BuildArgumentList(executable, "in.json", null, "models", null);

        Assert.DoesNotContain("out", args);
        Assert.Equal(new[] { "-m", "backend", "in.json", "--model-dir", "models" }, args);
    }

    [Fact]
    public void Bins_per_array_dimension_omitted_when_null()
    {
        var executable = new BackendExecutable("python", new[] { "-m", "backend" });

        var args = BackendCliRunner.BuildArgumentList(executable, "in.json", "out", "models", null);

        Assert.DoesNotContain("--bins-per-array-dimension", args);
    }
}
```

- [ ] **Step 2: Run the new tests, confirm they fail to compile**

Run: `dotnet test dotnet/BackendInterop.Tests/BackendInterop.Tests.csproj --filter BackendCliRunnerArgumentListTests`
Expected: build error — `BackendExecutable` and `BackendCliRunner.BuildArgumentList` don't exist yet.

- [ ] **Step 3: Replace `dotnet/BackendInterop/BackendCliRunner.cs` with:**

```csharp
using System.Diagnostics;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Result of one backend invocation. See
/// docs/superpowers/specs/2026-07-22-sub-project-2-backend-ui-interface-spec.md
/// for the full contract this mirrors.
/// </summary>
public sealed record BackendRunResult(int ExitCode, string StdOut, string StdErr)
{
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// Absolute path to processed_spectra.json, trimmed from stdout. Only
    /// meaningful when <see cref="IsSuccess"/> is true - stdout is not a
    /// path on failure.
    /// </summary>
    public string? OutputPath => IsSuccess ? StdOut.Trim() : null;
}

/// <summary>
/// The process to launch plus any args that must come before the shared
/// jsonIn/outDir/model-dir/bins args (S14). A python interpreter needs
/// `-m backend` prepended; a PyInstaller-frozen executable already *is*
/// the backend entrypoint and needs nothing prepended.
/// </summary>
public sealed record BackendExecutable(string FileName, IReadOnlyList<string> LeadingArgs);

/// <summary>
/// Shells out to the python backend's stable CLI contract
/// (`&lt;executable&gt; [leading-args] &lt;json_in&gt; [out_dir] --model-dir DIR
/// --bins-per-array-dimension N`). Does not decide what executable/leading
/// args to use - that's BackendEnvironment's job (cross-platform discovery,
/// dev vs packaged layout).
/// </summary>
public static class BackendCliRunner
{
    /// <param name="workingDirectory">
    /// In dev mode this must be the repo root (`python -m backend` needs
    /// `backend/` importable from CWD, since it isn't pip-installed). In
    /// packaged mode a frozen executable doesn't need this, but a real
    /// directory is still required by ProcessStartInfo.
    /// </param>
    public static BackendRunResult Run(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension = null)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, jsonIn, outDir, modelDir, workingDirectory, binsPerArrayDimension) };
        process.Start();

        // Read both streams before WaitForExit to avoid deadlock if either
        // pipe's buffer fills.
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new BackendRunResult(process.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    /// Async, cancellable counterpart of <see cref="Run"/> for S9's run
    /// flow (UI stays responsive, Cancel button kills the subprocess).
    /// Cancelling <paramref name="cancellationToken"/> kills the whole
    /// process tree (python may have spawned worker processes) and the
    /// call throws <see cref="OperationCanceledException"/> - it never
    /// returns a "cancelled" result value.
    /// </summary>
    public static async Task<BackendRunResult> RunAsync(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, jsonIn, outDir, modelDir, workingDirectory, binsPerArrayDimension) };

        using var killOnCancel = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited between HasExited check and Kill - fine, nothing to do.
            }
        });

        process.Start();

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        string stdOut = await stdOutTask;
        string stdErr = await stdErrTask;

        return new BackendRunResult(process.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    /// Pure argument-list construction, split out from BuildStartInfo so
    /// it's unit-testable without spawning a process (S14).
    /// </summary>
    public static IReadOnlyList<string> BuildArgumentList(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        int? binsPerArrayDimension)
    {
        var args = new List<string>(executable.LeadingArgs) { jsonIn };
        if (outDir is not null)
        {
            args.Add(outDir);
        }
        args.Add("--model-dir");
        args.Add(modelDir);
        if (binsPerArrayDimension is int bins)
        {
            args.Add("--bins-per-array-dimension");
            args.Add(bins.ToString());
        }

        return args;
    }

    private static ProcessStartInfo BuildStartInfo(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable.FileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // ArgumentList, never a concatenated command string - this is the
        // exact injection-prone pattern (Form1.cs's cmd.exe /c string build)
        // the S6 contract replaces.
        foreach (string arg in BuildArgumentList(executable, jsonIn, outDir, modelDir, binsPerArrayDimension))
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }
}
```

- [ ] **Step 4: Run the new tests, confirm they pass**

Run: `dotnet test dotnet/BackendInterop.Tests/BackendInterop.Tests.csproj --filter BackendCliRunnerArgumentListTests`
Expected: 4 passed.

- [ ] **Step 5: Fix `BackendCliRunnerIntegrationTests.cs`'s 4 broken call sites**

In `dotnet/BackendInterop.Tests/BackendCliRunnerIntegrationTests.cs`, every call to `BackendCliRunner.Run(python, ...)` / `RunAsync(python, ...)` now fails to compile because `python` is a `string?` and the first parameter is `BackendExecutable`. Add this local right after each `if (python is null) { return; }` guard (all 4 test methods):

```csharp
var executable = new BackendExecutable(python, new[] { "-m", "backend" });
```

Then replace `python` with `executable` in that method's `BackendCliRunner.Run(...)` / `RunAsync(...)` call (first argument).

- [ ] **Step 6: Fix `MainViewModel.cs`'s call site**

In `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs`, replace:

```csharp
        string? python = BackendEnvironment.PythonExecutable;
        if (python is null)
        {
            RunStatusText = "csp_modern python environment not found - cannot run.";
            return;
        }
```

with:

```csharp
        string? python = BackendEnvironment.PythonExecutable;
        if (python is null)
        {
            RunStatusText = "csp_modern python environment not found - cannot run.";
            return;
        }
        var executable = new BackendExecutable(python, new[] { "-m", "backend" });
```

and replace the `BackendCliRunner.RunAsync(` call's first argument `python,` with `executable,` (leave the rest of that call, including `BackendEnvironment.ModelDir` and `BackendEnvironment.RepoRoot`, untouched for now - Task 2 revisits this whole block).

- [ ] **Step 7: Build and test the whole solution**

Run: `dotnet build dotnet/CspAnalyzer.sln`
Expected: 0 errors.

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: all green (this dev box has the real `csp_modern` conda env, so the integration tests actually run, not skip).

- [ ] **Step 8: Commit**

```bash
git add dotnet/BackendInterop/BackendCliRunner.cs dotnet/BackendInterop.Tests/BackendCliRunnerArgumentListTests.cs dotnet/BackendInterop.Tests/BackendCliRunnerIntegrationTests.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs
git commit -m "S14: BackendExecutable type, BackendCliRunner takes executable+leading-args instead of a raw python path"
```

---

### Task 2: `FrozenBackendPaths` + packaged-mode wiring in `BackendEnvironment`

**Files:**
- Create: `dotnet/BackendInterop/FrozenBackendPaths.cs`
- Test (new): `dotnet/BackendInterop.Tests/FrozenBackendPathsTests.cs`
- Modify: `dotnet/BackendInterop/BackendEnvironment.cs`
- Modify: `dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs`
- Modify: `dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs` (finish the Task 1 stub)

**Interfaces:**
- Consumes: `BackendExecutable` from Task 1 (`dotnet/BackendInterop/BackendCliRunner.cs`).
- Produces: `FrozenBackendPaths.ExecutablePath(OSPlatform platform, string baseDirectory)`, `FrozenBackendPaths.ModelDir(string baseDirectory)`, `FrozenBackendPaths.IsPackagedLayout(OSPlatform platform, string baseDirectory)`.
- Produces: `BackendEnvironment.IsPackagedLayout`, `BackendEnvironment.Executable` (a `BackendExecutable?`, replaces direct `PythonExecutable` consumption in callers), `BackendEnvironment.WorkingDirectory` (replaces direct `RepoRoot` consumption in callers). `BackendEnvironment.PythonExecutable` and `BackendEnvironment.RepoRoot` still exist (dev-mode building blocks, `RepoRoot` now lazily computed instead of eagerly at type-init).

- [ ] **Step 1: Write the failing tests for `FrozenBackendPaths`**

Create `dotnet/BackendInterop.Tests/FrozenBackendPathsTests.cs`:

```csharp
using System.Runtime.InteropServices;
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

public class FrozenBackendPathsTests
{
    [Fact]
    public void ExecutablePath_windows_uses_exe_extension()
    {
        var path = FrozenBackendPaths.ExecutablePath(OSPlatform.Windows, "/base");

        Assert.EndsWith("csp-backend.exe", path);
    }

    [Fact]
    public void ExecutablePath_linux_has_no_extension()
    {
        var path = FrozenBackendPaths.ExecutablePath(OSPlatform.Linux, "/base");

        Assert.False(path.EndsWith(".exe"));
        Assert.EndsWith("csp-backend", path);
    }

    [Fact]
    public void ExecutablePath_osx_uses_same_shape_as_linux()
    {
        var linux = FrozenBackendPaths.ExecutablePath(OSPlatform.Linux, "/base");
        var osx = FrozenBackendPaths.ExecutablePath(OSPlatform.OSX, "/base");

        Assert.Equal(linux, osx);
    }

    [Fact]
    public void ModelDir_is_model_artifacts_under_base_directory()
    {
        var dir = FrozenBackendPaths.ModelDir("/base");

        Assert.Equal(Path.Combine("/base", "model_artifacts"), dir);
    }

    [Fact]
    public void IsPackagedLayout_false_for_an_empty_directory()
    {
        var baseDir = Directory.CreateTempSubdirectory("frozen_backend_layout_test_").FullName;

        Assert.False(FrozenBackendPaths.IsPackagedLayout(CurrentHostPlatform(), baseDir));
    }

    [Fact]
    public void IsPackagedLayout_true_when_dist_executable_and_model_artifacts_both_present()
    {
        var baseDir = Directory.CreateTempSubdirectory("frozen_backend_layout_test_").FullName;
        var platform = CurrentHostPlatform();
        var exePath = FrozenBackendPaths.ExecutablePath(platform, baseDir);
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(exePath, "");
        Directory.CreateDirectory(FrozenBackendPaths.ModelDir(baseDir));

        Assert.True(FrozenBackendPaths.IsPackagedLayout(platform, baseDir));
    }

    [Fact]
    public void IsPackagedLayout_false_when_only_executable_present_and_model_artifacts_missing()
    {
        var baseDir = Directory.CreateTempSubdirectory("frozen_backend_layout_test_").FullName;
        var platform = CurrentHostPlatform();
        var exePath = FrozenBackendPaths.ExecutablePath(platform, baseDir);
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(exePath, "");

        Assert.False(FrozenBackendPaths.IsPackagedLayout(platform, baseDir));
    }

    private static OSPlatform CurrentHostPlatform() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX
        : OSPlatform.Linux;
}
```

- [ ] **Step 2: Run tests, confirm they fail to compile**

Run: `dotnet test dotnet/BackendInterop.Tests/BackendInterop.Tests.csproj --filter FrozenBackendPathsTests`
Expected: build error — `FrozenBackendPaths` doesn't exist yet.

- [ ] **Step 3: Create `dotnet/BackendInterop/FrozenBackendPaths.cs`**

```csharp
using System.Runtime.InteropServices;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Pure path/detection logic for the packaged-install layout S14's
/// packaging produces: `model_artifacts/` and a PyInstaller-frozen
/// `csp-backend/csp-backend[.exe]` dist, both siblings of the app
/// executable. Takes baseDirectory + platform as parameters (not
/// auto-detected) so the exe-name-selection logic is unit-testable for
/// Windows from this Linux dev box - same reasoning as CondaPythonPaths
/// (S11). The existence checks themselves inherently run against whatever
/// the real host filesystem is (there's no simulating a foreign OS's
/// filesystem), so those are tested against a real temp directory on
/// whichever OS the test happens to run on - the 3-OS CI matrix (S13)
/// gives real cross-platform coverage of that half for free.
/// </summary>
public static class FrozenBackendPaths
{
    private const string DistDirName = "csp-backend";
    private const string ModelArtifactsDirName = "model_artifacts";

    public static string ExecutablePath(OSPlatform platform, string baseDirectory)
    {
        string exeName = platform == OSPlatform.Windows ? "csp-backend.exe" : "csp-backend";
        return Path.Combine(baseDirectory, DistDirName, exeName);
    }

    public static string ModelDir(string baseDirectory) => Path.Combine(baseDirectory, ModelArtifactsDirName);

    public static bool IsPackagedLayout(OSPlatform platform, string baseDirectory) =>
        File.Exists(ExecutablePath(platform, baseDirectory)) && Directory.Exists(ModelDir(baseDirectory));
}
```

- [ ] **Step 4: Run tests, confirm they pass**

Run: `dotnet test dotnet/BackendInterop.Tests/BackendInterop.Tests.csproj --filter FrozenBackendPathsTests`
Expected: 7 passed.

- [ ] **Step 5: Rewrite `dotnet/BackendInterop/BackendEnvironment.cs`**

Replace the whole file with:

```csharp
using System.Runtime.InteropServices;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Runtime discovery so CspAnalyzer.Desktop can invoke BackendCliRunner
/// without every call site hardcoding paths (S9). Two layouts are
/// supported: a packaged install (model_artifacts/ + a frozen csp-backend
/// dist sitting as siblings of the app under AppContext.BaseDirectory -
/// S14) is tried first; a dev checkout (.git walk-up + csp_modern conda
/// probe - S11) is the fallback. RepoRoot is lazily computed (not a field
/// initializer) specifically so that touching this class in a packaged
/// install - where there's no .git anywhere above the app - never throws
/// unless something actually needs the dev-mode fallback.
/// </summary>
public static class BackendEnvironment
{
    private static string? _repoRoot;

    /// <summary>
    /// Only valid in a dev checkout. Lazily computed so merely referencing
    /// other members of this class (e.g. IsPackagedLayout) never triggers
    /// this walk-up in a packaged install that has no .git directory.
    /// </summary>
    public static string RepoRoot => _repoRoot ??= FindRepoRoot();

    public static bool IsPackagedLayout => FrozenBackendPaths.IsPackagedLayout(CurrentPlatform(), AppContext.BaseDirectory);

    public static string ModelDir => IsPackagedLayout
        ? FrozenBackendPaths.ModelDir(AppContext.BaseDirectory)
        : Path.Combine(RepoRoot, "backend", "model_artifacts");

    /// <summary>
    /// ProcessStartInfo.WorkingDirectory for BackendCliRunner. Dev mode
    /// needs the repo root (`python -m backend` needs backend/ importable
    /// from CWD). Packaged mode's frozen executable has no such
    /// requirement, so the app's own directory is fine.
    /// </summary>
    public static string WorkingDirectory => IsPackagedLayout ? AppContext.BaseDirectory : RepoRoot;

    /// <summary>
    /// Path to the csp_modern conda env's python, or null if that exact
    /// env isn't present on the current machine under any known conda
    /// distro (miniforge3/miniconda3/anaconda3). Dev-mode only - packaged
    /// installs don't use this.
    /// </summary>
    public static string? PythonExecutable
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return CondaPythonPaths.BuildCandidates(CurrentPlatform(), home, "csp_modern")
                .FirstOrDefault(File.Exists);
        }
    }

    /// <summary>
    /// The BackendExecutable callers should pass to BackendCliRunner, or
    /// null if no backend is reachable (packaged install with a missing
    /// dist is not expected, but dev checkout without csp_modern
    /// installed is a real, user-facing case the caller must handle).
    /// </summary>
    public static BackendExecutable? Executable
    {
        get
        {
            if (IsPackagedLayout)
            {
                return new BackendExecutable(
                    FrozenBackendPaths.ExecutablePath(CurrentPlatform(), AppContext.BaseDirectory),
                    Array.Empty<string>());
            }

            string? python = PythonExecutable;
            return python is null ? null : new BackendExecutable(python, new[] { "-m", "backend" });
        }
    }

    private static OSPlatform CurrentPlatform() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX
        : OSPlatform.Linux;

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

- [ ] **Step 6: Add a packaged-layout regression test to `BackendEnvironmentTests.cs`**

Add this test to `dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs` (inside the existing `BackendEnvironmentTests` class):

```csharp
    [Fact]
    public void IsPackagedLayout_is_false_in_this_dev_checkout()
    {
        // This test assembly's own output directory has no csp-backend/
        // dist or model_artifacts/ sitting next to it - only a real S14
        // package layout would make this true.
        Assert.False(BackendEnvironment.IsPackagedLayout);
    }

    [Fact]
    public void Executable_in_dev_checkout_matches_PythonExecutable_with_module_leading_args()
    {
        var executable = BackendEnvironment.Executable;
        var python = BackendEnvironment.PythonExecutable;

        if (python is null)
        {
            Assert.Null(executable);
        }
        else
        {
            Assert.NotNull(executable);
            Assert.Equal(python, executable!.FileName);
            Assert.Equal(new[] { "-m", "backend" }, executable.LeadingArgs);
        }
    }
```

- [ ] **Step 7: Finish `MainViewModel.cs`'s `RunAsync` method**

Replace the Task-1 stub block:

```csharp
        string? python = BackendEnvironment.PythonExecutable;
        if (python is null)
        {
            RunStatusText = "csp_modern python environment not found - cannot run.";
            return;
        }
        var executable = new BackendExecutable(python, new[] { "-m", "backend" });
```

with:

```csharp
        BackendExecutable? executable = BackendEnvironment.Executable;
        if (executable is null)
        {
            RunStatusText = "csp_modern python environment not found - cannot run.";
            return;
        }
```

Then find the `BackendCliRunner.RunAsync(` call a few lines below and replace its `BackendEnvironment.RepoRoot` argument with `BackendEnvironment.WorkingDirectory` (the `executable,` and `BackendEnvironment.ModelDir,` arguments are already correct from Task 1 / already unchanged).

- [ ] **Step 8: Build and test the whole solution**

Run: `dotnet build dotnet/CspAnalyzer.sln`
Expected: 0 errors.

Run: `dotnet test dotnet/CspAnalyzer.sln`
Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add dotnet/BackendInterop/FrozenBackendPaths.cs dotnet/BackendInterop/BackendEnvironment.cs dotnet/BackendInterop.Tests/FrozenBackendPathsTests.cs dotnet/BackendInterop.Tests/BackendEnvironmentTests.cs dotnet/CspAnalyzer.Desktop/ViewModels/MainViewModel.cs
git commit -m "S14: packaged-mode path resolution (FrozenBackendPaths, BackendEnvironment.Executable/WorkingDirectory), lazy RepoRoot"
```

---

### Task 3: PyInstaller entrypoint + spec file

**Files:**
- Create: `backend/scripts/pyinstaller_entrypoint.py`
- Create: `backend/csp-backend.spec`
- Modify: `backend/requirements-dev.txt`
- Modify: `.gitignore`

**Interfaces:**
- Produces: `backend/csp-backend.spec` — a PyInstaller spec, run as `pyinstaller backend/csp-backend.spec` from the repo root, producing `backend/dist/csp-backend/csp-backend` (or `.exe` on Windows), matching exactly what `FrozenBackendPaths` (Task 2) expects to find once copied into a package.

- [ ] **Step 1: Add the PyInstaller entrypoint script**

Create `backend/scripts/pyinstaller_entrypoint.py`:

```python
"""Entry point PyInstaller freezes into the `csp-backend` executable.

Equivalent to `python -m backend`, but PyInstaller needs a real script (not
a `-m` module invocation) as its Analysis entry point.
"""
import sys

from backend.__main__ import main

if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Add the PyInstaller spec file**

Create `backend/csp-backend.spec` (run from repo root as `pyinstaller backend/csp-backend.spec`):

```python
# PyInstaller spec for S14 packaging. Freezes backend/ (+ numpy/scipy/
# scikit-learn/scikit-image) into a standalone `csp-backend` executable.
# Run from the repo root: `pyinstaller backend/csp-backend.spec`
#
# The hiddenimports list below is a best-effort starting point for known
# PyInstaller/sklearn/skimage gotchas (both packages dynamically dispatch
# into compiled Cython extension modules PyInstaller's static analysis can
# miss). Task 4 runs the real freeze and fixes this list against whatever
# ModuleNotFoundError actually shows up - don't trust this list as final
# without that verification step.
from pathlib import Path

REPO_ROOT = Path(SPECPATH).parent

a = Analysis(
    ['scripts/pyinstaller_entrypoint.py'],
    pathex=[str(REPO_ROOT)],
    binaries=[],
    datas=[],
    hiddenimports=[
        'sklearn.utils._typedefs',
        'sklearn.utils._heap',
        'sklearn.utils._sorting',
        'sklearn.utils._vector_sentinel',
        'sklearn.neighbors._partition_nodes',
        'skimage.feature._orb_descriptor_positions',
        'scipy.special.cython_special',
    ],
    hookspath=[],
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='csp-backend',
    debug=False,
    strip=False,
    upx=False,
    console=True,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    name='csp-backend',
)
```

- [ ] **Step 3: Pin PyInstaller in `backend/requirements-dev.txt`**

Current content is:

```
-r requirements.txt
pytest==9.1.1
```

Replace with:

```
-r requirements.txt
pytest==9.1.1
pyinstaller==6.21.0
```

- [ ] **Step 4: Ignore PyInstaller's build output in `.gitignore`**

Add this block to `.gitignore` right after the existing `# CSP-Analyzer: bundled runtime + legacy model pickles + demo data` section:

```
# PyInstaller build output (S14 - frozen per-OS, not source, rebuilt in CI)
backend/build/
backend/dist/
```

- [ ] **Step 5: Commit**

```bash
git add backend/scripts/pyinstaller_entrypoint.py backend/csp-backend.spec backend/requirements-dev.txt .gitignore
git commit -m "S14: PyInstaller entrypoint + spec for freezing the python backend"
```

---

### Task 4: Build and verify the frozen backend for real (Linux)

This dev box can only verify the Linux leg locally (Windows/macOS freezes happen in CI, Task 6/8) - matches the spec's stated testing approach. The `hiddenimports` list from Task 3 is a best guess; this task's job is to make it actually correct by running the real freeze and fixing whatever breaks.

**Files:**
- Modify: `backend/csp-backend.spec` (iteratively, if `hiddenimports` needs additions)

- [ ] **Step 1: Install pyinstaller into the `csp_modern` conda env and freeze**

```bash
conda activate csp_modern
pip install -r backend/requirements-dev.txt
cd backend && pyinstaller csp-backend.spec --noconfirm && cd ..
```

Expected: completes without error, produces `backend/dist/csp-backend/csp-backend`.

- [ ] **Step 2: Run the frozen executable against the committed demo fixture**

```bash
./backend/dist/csp-backend/csp-backend \
  dotnet/BackendInterop.Tests/Fixtures/demo.json \
  /tmp/csp_frozen_test_out \
  --model-dir backend/model_artifacts \
  --bins-per-array-dimension 128
```

Expected: exit code 0, stdout is exactly one line (the output path), and `/tmp/csp_frozen_test_out/processed_spectra.json` exists.

- [ ] **Step 3: If it fails with `ModuleNotFoundError: No module named 'X'`, fix and retry**

Add `'X'` (and any transitively-needed submodule reported next) to the `hiddenimports` list in `backend/csp-backend.spec`, then:

```bash
rm -rf backend/build backend/dist
cd backend && pyinstaller csp-backend.spec --noconfirm && cd ..
```

Repeat Steps 2-3 until Step 2 succeeds cleanly. Note in the Task 5 commit message (or a follow-up commit here) which modules actually needed adding, since this list is genuinely dev-box-specific knowledge worth preserving for future sessions touching this spec file.

- [ ] **Step 4: Validate the output JSON matches the real python's shape**

```bash
python -c "
import json
with open('/tmp/csp_frozen_test_out/processed_spectra.json') as f:
    data = json.load(f)
assert isinstance(data, list) and len(data) > 0, data
assert 'proba' in data[0] and 'is_active' in data[0], data[0]
print('OK', len(data), 'results')
"
```

Expected: prints `OK <N> results` with no assertion error.

- [ ] **Step 5: Commit any `hiddenimports` fixes from Step 3**

```bash
git add backend/csp-backend.spec
git commit -m "S14: fix PyInstaller hiddenimports found by a real freeze+run"
```

(Skip this commit if Step 1's freeze already worked with no changes needed.)

---

### Task 5: Packaging script

**Files:**
- Create: `scripts/package/package.ps1`

**Interfaces:**
- Consumes: `dotnet publish` output at `dotnet/CspAnalyzer.Desktop/bin/Release/net8.0/<rid>/publish/`, `backend/model_artifacts/`, `backend/dist/csp-backend/` (Task 4's output).
- Produces: `artifacts/CspAnalyzer-<os-name>-<rid>.zip`.

One script, not per-OS bash/batch variants: `pwsh` (PowerShell 7) is preinstalled on all three GitHub-hosted runner images (`ubuntu-latest`, `windows-latest`, `macos-latest`), so a single script runs unmodified on every CI matrix leg. This dev box doesn't have `pwsh` installed locally, so this script's real execution is verified in Task 6/8's actual CI run, not locally - review the code carefully since it can't be dry-run here.

- [ ] **Step 1: Create `scripts/package/package.ps1`**

```powershell
<#
.SYNOPSIS
  Assembles a self-contained CspAnalyzer package: dotnet publish output +
  model_artifacts/ + the PyInstaller-frozen csp-backend dist, then zips it.
  Run from the repo root, after both `dotnet publish` and `pyinstaller`
  have already produced their outputs (the CI packaging job runs both
  first - see .github/workflows/ci.yml's `package` job).
#>
param(
    [Parameter(Mandatory = $true)][string]$Rid,      # e.g. win-x64, linux-x64, osx-x64
    [Parameter(Mandatory = $true)][string]$OsName    # e.g. windows, linux, macos - used only in the output filename
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$publishDir = Join-Path $repoRoot "dotnet/CspAnalyzer.Desktop/bin/Release/net8.0/$Rid/publish"
$modelArtifactsDir = Join-Path $repoRoot "backend/model_artifacts"
$frozenBackendDistDir = Join-Path $repoRoot "backend/dist/csp-backend"
$artifactsDir = Join-Path $repoRoot "artifacts"
$outputDir = Join-Path $artifactsDir "CspAnalyzer-$OsName-$Rid"
$zipPath = Join-Path $artifactsDir "CspAnalyzer-$OsName-$Rid.zip"

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found at $publishDir - run 'dotnet publish -r $Rid --self-contained' first."
}
if (-not (Test-Path $frozenBackendDistDir)) {
    throw "Frozen backend dist not found at $frozenBackendDistDir - run 'pyinstaller backend/csp-backend.spec' first."
}
if (-not (Test-Path $modelArtifactsDir)) {
    throw "model_artifacts/ not found at $modelArtifactsDir."
}

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Copy-Item -Path "$publishDir/*" -Destination $outputDir -Recurse -Force
Copy-Item -Path $modelArtifactsDir -Destination (Join-Path $outputDir "model_artifacts") -Recurse -Force
Copy-Item -Path $frozenBackendDistDir -Destination (Join-Path $outputDir "csp-backend") -Recurse -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}
Compress-Archive -Path "$outputDir/*" -DestinationPath $zipPath

Write-Host "Packaged: $zipPath"
```

- [ ] **Step 2: Commit**

```bash
git add scripts/package/package.ps1
git commit -m "S14: cross-platform packaging script (pwsh, runs on all 3 CI runner images)"
```

---

### Task 6: CI packaging job

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `backend/csp-backend.spec` (Task 3), `scripts/package/package.ps1` (Task 5).

- [ ] **Step 1: Add `workflow_dispatch` to the workflow's triggers**

Current top of `.github/workflows/ci.yml`:

```yaml
on:
  push:
    branches: [master]
  pull_request:
```

Replace with:

```yaml
on:
  push:
    branches: [master]
  pull_request:
  workflow_dispatch:
```

(This also lets `python-tests`/`dotnet-tests` be triggered manually, which is harmless - a manual "build a package" run re-verifying the test suite first is a reasonable side effect, not a bug.)

- [ ] **Step 2: Add the `package` job**

Append this job to `.github/workflows/ci.yml` (same indentation level as `python-tests`/`dotnet-tests`):

```yaml
  package:
    if: github.event_name == 'workflow_dispatch'
    strategy:
      fail-fast: false
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
            os-name: linux
          - os: windows-latest
            rid: win-x64
            os-name: windows
          - os: macos-latest
            rid: osx-x64
            os-name: macos
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

      - name: Freeze python backend with PyInstaller
        working-directory: backend
        run: pyinstaller csp-backend.spec --noconfirm

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Publish self-contained desktop app
        run: dotnet publish dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj -c Release -r ${{ matrix.rid }} --self-contained

      - name: Assemble package
        shell: pwsh
        run: ./scripts/package/package.ps1 -Rid ${{ matrix.rid }} -OsName ${{ matrix.os-name }}

      - uses: actions/upload-artifact@v4
        with:
          name: CspAnalyzer-${{ matrix.os-name }}-${{ matrix.rid }}
          path: artifacts/CspAnalyzer-${{ matrix.os-name }}-${{ matrix.rid }}.zip
```

If the `dotnet publish -r ... --self-contained` step fails during Task 8's real CI run complaining it can't resolve the runtime identifier, add this to `dotnet/CspAnalyzer.Desktop/CspAnalyzer.Desktop.csproj`'s first `<PropertyGroup>` and retry:

```xml
    <RuntimeIdentifiers>win-x64;linux-x64;osx-x64</RuntimeIdentifiers>
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "S14: workflow_dispatch packaging job, 3-OS matrix, uploads per-OS zip artifacts"
```

---

### Task 7: README documentation

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add an "Installation" section**

In `README.md`, insert this new section right after the `## Overview` section (i.e. right before the existing `## How It Works` heading, currently at line 19):

```markdown
## Installation

Pre-built, self-contained packages are available for Linux, Windows, and
macOS (x64) - no separate Python or .NET install required, the backend
classifier is bundled inside.

1. Download the zip for your OS from the latest packaging run (Actions →
   "CI" → the most recent `workflow_dispatch` run → Artifacts).
2. Extract it anywhere.
3. Run the executable directly from the extracted folder:
   - **Windows:** `CspAnalyzer.Desktop.exe`
   - **Linux / macOS:** `./CspAnalyzer.Desktop`

Don't move the executable out of its extracted folder - it expects
`model_artifacts/` and `csp-backend/` to stay right next to it.

Building from source instead requires the .NET 8 SDK and a `csp_modern`
conda environment (see `backend/requirements.txt`) - see
`docs/superpowers/SESSIONS.md` for the full development setup this project
was built with.
```

- [ ] **Step 2: Rewrite the obsolete "No Python.exe found" troubleshooting entry**

In `README.md`, replace:

```markdown
### No Python.exe found

Check that the Miniconda environment files are in the same folder from where the application was launched. If missing, manually copy the Miniconda3 folder to the application start path. Also check that the `pickle_jar` folder (containing the ML training repo) is there.
```

with:

```markdown
### Backend not found / "csp_modern python environment not found"

If you're running a downloaded package: don't move the executable out of
its extracted folder - the bundled backend (`csp-backend/`) and
`model_artifacts/` must stay right next to it.

If you're running from source (a dev checkout, not a downloaded package):
this means no `csp_modern` conda environment was found. Install
Miniconda/Miniforge and create the env described in
`backend/requirements.txt`.
```

- [ ] **Step 3: Add a "Background" section synopsizing the paper**

In `README.md`, insert this new section right before the existing `## Citation` heading:

```markdown
## Background

CSP-Analyzer implements the method described in the paper below. Fragment-based
drug discovery relies on NMR screening to detect chemical shift perturbations
(CSPs) that indicate protein-ligand binding, but manually reviewing hundreds
of 2D spectra per campaign is slow and inconsistent - the same spectrum can
get classified differently depending on where it falls in a long review
session.

The approach: each 2D HSQC spectrum is reduced to a 15-element descriptor
vector by comparing it against a reference spectrum using computer-vision
techniques (histograms of oriented gradients, phase cross-correlation
registration, ORB point-matching, structural similarity, Hu moments,
MSE/PSNR, and Jensen-Shannon entropy). SMOTE-ENN balances the training
classes, PCA reduces dimensionality, and an RBF-kernel SVM (with Platt
scaling for calibrated probabilities) classifies each spectrum as active or
inactive.

Validated on 1,611 2D HSQC spectra across 4 protein targets, trained on
just 100 labeled spectra (6.2% of the total): 0.87 average accuracy, 0.72
sensitivity, 0.88 specificity, 3.10% false-negative rate, 10.30%
false-positive rate - deliberately tuned to minimize missed actives over
minimizing false alarms.
```

- [ ] **Step 4: Proofread**

Re-read the modified `README.md` end to end. Confirm: the new sections render as valid markdown (headings, code fences, lists), no broken internal structure, the existing `## Citation`/`## License`/`## Authors`/`## Funding` sections are unchanged and still immediately follow `## Background`.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "S14: install instructions for packaged builds, paper synopsis, drop obsolete Miniconda-copy troubleshooting"
```

---

### Task 8: Trigger and verify a real CI packaging run

**This task pushes to the remote and dispatches a GitHub Actions workflow - confirm with the user before running any push/dispatch command here**, per this project's git safety norms (S13's session needed an explicit push decision for the same reason).

- [ ] **Step 1: Confirm with the user before proceeding**

Ask: "Ready to push the S14 branch/commits and manually trigger the new `package` workflow job (3 real CI runners, produces real per-OS zip artifacts)? This is the only way to verify the Windows/macOS legs - this dev box can only build Linux locally."

- [ ] **Step 2: Push and dispatch**

```bash
git push
gh workflow run ci.yml --ref <branch-name>
```

- [ ] **Step 3: Watch the run**

```bash
gh run list --workflow=ci.yml --limit 1
gh run watch <run-id>
```

- [ ] **Step 4: Verify all matrix legs succeeded and produced artifacts**

```bash
gh run view <run-id>
```

Expected: `package (ubuntu-latest, linux-x64, linux)`, `package (windows-latest, win-x64, windows)`, `package (macos-latest, osx-x64, macos)` all green, each with an uploaded artifact.

If any leg fails, read its log (`gh run view <run-id> --log-failed`) - likely failure modes given this plan's earlier notes: a `ModuleNotFoundError` on a platform Task 4 couldn't test locally (fix `hiddenimports` in `backend/csp-backend.spec`, same process as Task 4 Step 3), or a `dotnet publish` RID-resolution error (apply the `RuntimeIdentifiers` fix noted in Task 6 Step 2).

- [ ] **Step 5: Download and manually sanity-check at least one artifact**

```bash
gh run download <run-id> -n CspAnalyzer-linux-linux-x64 -D /tmp/csp_package_check
cd /tmp/csp_package_check && unzip CspAnalyzer-linux-linux-x64.zip -d extracted
DISPLAY=:0 ./extracted/CspAnalyzer.Desktop
```

Expected: the app launches (this dev box has a display, per S7-S10's manual verification pattern). Confirm `model_artifacts/` and `csp-backend/` are present as siblings in `extracted/`.

---

## Self-Review Notes

- **Spec coverage:** every decision in the design spec (bundle standalone python; `AppContext.BaseDirectory`-relative packaged layout; probe-then-fallback mode detection; self-contained zip, not installers; `workflow_dispatch`-only CI; README install docs + paper synopsis) has a task above.
- **Type consistency check:** `BackendExecutable` (Task 1) is consumed identically in Task 2's `BackendEnvironment.Executable` and Task 1's own `BackendCliRunnerIntegrationTests`/`MainViewModel` fixes. `FrozenBackendPaths` (Task 2) method names/signatures match between its own implementation and its test file. `csp-backend` dist/executable naming is identical across `FrozenBackendPaths.cs` (Task 2), `csp-backend.spec` (Task 3), `package.ps1` (Task 5), and `ci.yml` (Task 6) - all four independently reference the same `csp-backend`/`csp-backend.exe` name, checked for consistency.
- **No placeholders:** every step above either contains real, complete code or a real, runnable shell command - the two spots that defer a decision (Task 3's `hiddenimports` list, Task 6's `RuntimeIdentifiers` fallback) both ship a concrete starting point plus the exact fix to apply if that starting point turns out wrong, rather than leaving either unresolved.
