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
