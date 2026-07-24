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
