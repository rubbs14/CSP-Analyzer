namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Minimal runtime discovery so CspAnalyzer.Desktop can actually invoke
/// BackendCliRunner without every call site hardcoding paths (S9). Same
/// walk-up-from-assembly strategy as this project's test helper
/// (BackendInterop.Tests/RepoPaths.cs) and same python-discovery caveat -
/// real cross-platform env/python discovery (installer-relative paths,
/// PATH search, Windows/Mac conda locations) is S11's job.
/// </summary>
public static class BackendEnvironment
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ModelDir => Path.Combine(RepoRoot, "backend", "model_artifacts");

    /// <summary>
    /// Path to the csp_modern conda env's python, or null if that exact
    /// env isn't present on the current machine.
    /// </summary>
    public static string? PythonExecutable
    {
        get
        {
            var candidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "miniforge3", "envs", "csp_modern", "bin", "python");
            return File.Exists(candidate) ? candidate : null;
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
