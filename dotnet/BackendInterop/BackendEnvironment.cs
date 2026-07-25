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
    /// Path to a python interpreter with the backend's dependencies
    /// installed, or null if none is discoverable. Dev-mode only - packaged
    /// installs don't use this. Checks the CSP_ANALYZER_PYTHON environment
    /// variable first - CI runners have no csp_modern conda env (the
    /// conda-path guess below never finds anything there), so the
    /// dotnet-tests CI job sets this to whatever interpreter it just pip
    /// installed backend/requirements.txt into. Falls back to probing for
    /// the csp_modern conda env under any known conda distro
    /// (miniforge3/miniconda3/anaconda3) for real dev-machine use.
    /// </summary>
    public static string? PythonExecutable
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("CSP_ANALYZER_PYTHON");
            if (!string.IsNullOrEmpty(overridePath))
            {
                return overridePath;
            }

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
