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
