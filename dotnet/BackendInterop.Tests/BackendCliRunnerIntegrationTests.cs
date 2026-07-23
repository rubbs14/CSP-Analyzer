using System.Diagnostics;
using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

/// <summary>
/// Exercises the real S6 contract end to end: shells out to the actual
/// csp_modern conda env's python, against the real repo model_artifacts/,
/// with a committed demo fixture. Skips itself if that specific conda env
/// isn't present - cross-platform python/env discovery lives in
/// BackendEnvironment/CondaPythonPaths, not this stub.
/// </summary>
public class BackendCliRunnerIntegrationTests
{
    private const int DemoBins = 128; // must match backend/tests/helpers.py:DEMO_BINS - the fixture was generated with it.

    [Fact]
    public void Run_against_real_backend_returns_success_exit_code_and_parseable_output()
    {
        var python = RepoPaths.CspModernPythonExecutable;
        if (python is null)
        {
            return; // csp_modern conda env not present on this machine - see class doc.
        }

        var jsonIn = Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo.json");
        var outDir = Directory.CreateTempSubdirectory("backend_interop_test_").FullName;

        var result = BackendCliRunner.Run(
            python,
            jsonIn,
            outDir,
            RepoPaths.RealModelArtifactsDir,
            RepoPaths.RepoRoot,
            binsPerArrayDimension: DemoBins);

        Assert.True(result.IsSuccess, $"exit={result.ExitCode}, stderr={result.StdErr}");
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        var parsed = SpectrumResult.ParseArray(File.ReadAllText(result.OutputPath!));
        Assert.NotEmpty(parsed);
    }

    [Fact]
    public void Run_with_bad_json_in_returns_exit_1_and_stderr_message_no_traceback()
    {
        var python = RepoPaths.CspModernPythonExecutable;
        if (python is null)
        {
            return;
        }

        var result = BackendCliRunner.Run(
            python,
            "/no/such/file.json",
            null,
            RepoPaths.RealModelArtifactsDir,
            RepoPaths.RepoRoot);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("Error: ", result.StdErr);
        Assert.Null(result.OutputPath);
    }

    [Fact]
    public async Task RunAsync_against_real_backend_returns_success_exit_code_and_parseable_output()
    {
        var python = RepoPaths.CspModernPythonExecutable;
        if (python is null)
        {
            return;
        }

        var jsonIn = Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo.json");
        var outDir = Directory.CreateTempSubdirectory("backend_interop_async_test_").FullName;

        var result = await BackendCliRunner.RunAsync(
            python,
            jsonIn,
            outDir,
            RepoPaths.RealModelArtifactsDir,
            RepoPaths.RepoRoot,
            DemoBins,
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"exit={result.ExitCode}, stderr={result.StdErr}");
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        var parsed = SpectrumResult.ParseArray(File.ReadAllText(result.OutputPath!));
        Assert.NotEmpty(parsed);
    }

    [Fact]
    public async Task RunAsync_cancelled_before_completion_throws_and_stops_quickly()
    {
        var python = RepoPaths.CspModernPythonExecutable;
        if (python is null)
        {
            return;
        }

        var jsonIn = Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo.json");
        var outDir = Directory.CreateTempSubdirectory("backend_interop_cancel_test_").FullName;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BackendCliRunner.RunAsync(
                python,
                jsonIn,
                outDir,
                RepoPaths.RealModelArtifactsDir,
                RepoPaths.RepoRoot,
                DemoBins,
                cts.Token));
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"cancellation took too long: {sw.Elapsed}");
    }
}
