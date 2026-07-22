using System.Diagnostics;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Result of one `python -m backend ...` invocation. See
/// docs/superpowers/specs/2026-07-22-sub-project-2-backend-ui-interface-spec.md
/// for the full contract this mirrors.
/// </summary>
public sealed record BackendRunResult(int ExitCode, string StdOut, string StdErr)
{
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// Absolute path to processed_spectra.json, trimmed from stdout. Only
    /// meaningful when <see cref="IsSuccess"/> is true - stdout is not a
    /// path on failure.
    /// </summary>
    public string? OutputPath => IsSuccess ? StdOut.Trim() : null;
}

/// <summary>
/// Shells out to the python backend's stable CLI contract
/// (`python -m backend &lt;json_in&gt; [out_dir] --model-dir DIR
/// --bins-per-array-dimension N`). Does not search for a python executable -
/// the caller resolves that (cross-platform discovery is S11's job).
/// </summary>
public static class BackendCliRunner
{
    /// <param name="workingDirectory">
    /// Directory containing the `backend/` package (i.e. the repo root), so
    /// `python -m backend` can find it - `backend` isn't pip-installed, so
    /// this can't be left to whatever CWD the caller process happens to
    /// have, same reasoning as <paramref name="modelDir"/> needing to be
    /// absolute.
    /// </param>
    public static BackendRunResult Run(
        string pythonExecutable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // ArgumentList, never a concatenated command string - this is the
        // exact injection-prone pattern (Form1.cs's cmd.exe /c string build)
        // the S6 contract replaces.
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add("backend");
        startInfo.ArgumentList.Add(jsonIn);
        if (outDir is not null)
        {
            startInfo.ArgumentList.Add(outDir);
        }
        startInfo.ArgumentList.Add("--model-dir");
        startInfo.ArgumentList.Add(modelDir);
        if (binsPerArrayDimension is int bins)
        {
            startInfo.ArgumentList.Add("--bins-per-array-dimension");
            startInfo.ArgumentList.Add(bins.ToString());
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read both streams before WaitForExit to avoid deadlock if either
        // pipe's buffer fills.
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new BackendRunResult(process.ExitCode, stdOut, stdErr);
    }
}
