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
