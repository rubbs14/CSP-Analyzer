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
