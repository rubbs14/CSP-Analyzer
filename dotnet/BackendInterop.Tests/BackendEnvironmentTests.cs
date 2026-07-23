using Xunit;

namespace CspAnalyzer.BackendInterop.Tests;

/// <summary>
/// BackendEnvironment is the app-runtime counterpart of this test project's
/// own RepoPaths helper - same repo-root discovery strategy (walk up from
/// the running assembly looking for backend/ + .git/), but living in
/// BackendInterop so CspAnalyzer.Desktop can use it too. PythonExecutable
/// probes cross-platform conda env locations (S11); RepoPaths now delegates
/// to it instead of duplicating the logic.
/// </summary>
public class BackendEnvironmentTests
{
    [Fact]
    public void RepoRoot_contains_backend_package_and_git_dir()
    {
        var root = BackendEnvironment.RepoRoot;

        Assert.True(Directory.Exists(Path.Combine(root, "backend")));
        Assert.True(Directory.Exists(Path.Combine(root, ".git")));
    }

    [Fact]
    public void ModelDir_points_at_the_real_checked_in_model_artifacts_dir()
    {
        var modelDir = BackendEnvironment.ModelDir;

        Assert.Equal(Path.Combine(BackendEnvironment.RepoRoot, "backend", "model_artifacts"), modelDir);
        Assert.True(Directory.Exists(modelDir));
    }

    [Fact]
    public void PythonExecutable_is_null_or_an_existing_file()
    {
        // Can't assert presence (machine-dependent) - only that the
        // contract (null when absent, a real existing path when present)
        // holds. Candidate-path shape itself is covered by
        // CondaPythonPathsTests, which doesn't depend on the real machine.
        var python = BackendEnvironment.PythonExecutable;

        if (python is not null)
        {
            Assert.True(File.Exists(python));
        }
    }
}
