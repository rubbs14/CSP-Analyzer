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
