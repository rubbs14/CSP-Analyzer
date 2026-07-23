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
