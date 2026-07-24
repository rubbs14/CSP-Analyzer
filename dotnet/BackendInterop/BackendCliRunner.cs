using System.Diagnostics;

namespace CspAnalyzer.BackendInterop;

/// <summary>
/// Result of one backend invocation. See
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
/// The process to launch plus any args that must come before the shared
/// jsonIn/outDir/model-dir/bins args (S14). A python interpreter needs
/// `-m backend` prepended; a PyInstaller-frozen executable already *is*
/// the backend entrypoint and needs nothing prepended.
/// </summary>
public sealed record BackendExecutable(string FileName, IReadOnlyList<string> LeadingArgs);

/// <summary>
/// Shells out to the python backend's stable CLI contract
/// (`&lt;executable&gt; [leading-args] &lt;json_in&gt; [out_dir] --model-dir DIR
/// --bins-per-array-dimension N`). Does not decide what executable/leading
/// args to use - that's BackendEnvironment's job (cross-platform discovery,
/// dev vs packaged layout).
/// </summary>
public static class BackendCliRunner
{
    /// <param name="workingDirectory">
    /// In dev mode this must be the repo root (`python -m backend` needs
    /// `backend/` importable from CWD, since it isn't pip-installed). In
    /// packaged mode a frozen executable doesn't need this, but a real
    /// directory is still required by ProcessStartInfo.
    /// </param>
    public static BackendRunResult Run(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension = null)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, jsonIn, outDir, modelDir, workingDirectory, binsPerArrayDimension) };
        process.Start();

        // Read both streams before WaitForExit to avoid deadlock if either
        // pipe's buffer fills.
        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new BackendRunResult(process.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    /// Async, cancellable counterpart of <see cref="Run"/> for S9's run
    /// flow (UI stays responsive, Cancel button kills the subprocess).
    /// Cancelling <paramref name="cancellationToken"/> kills the whole
    /// process tree (python may have spawned worker processes) and the
    /// call throws <see cref="OperationCanceledException"/> - it never
    /// returns a "cancelled" result value.
    /// </summary>
    public static async Task<BackendRunResult> RunAsync(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = BuildStartInfo(executable, jsonIn, outDir, modelDir, workingDirectory, binsPerArrayDimension) };

        using var killOnCancel = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited between HasExited check and Kill - fine, nothing to do.
            }
        });

        process.Start();

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        string stdOut = await stdOutTask;
        string stdErr = await stdErrTask;

        return new BackendRunResult(process.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    /// Pure argument-list construction, split out from BuildStartInfo so
    /// it's unit-testable without spawning a process (S14).
    /// </summary>
    public static IReadOnlyList<string> BuildArgumentList(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        int? binsPerArrayDimension)
    {
        var args = new List<string>(executable.LeadingArgs) { jsonIn };
        if (outDir is not null)
        {
            args.Add(outDir);
        }
        args.Add("--model-dir");
        args.Add(modelDir);
        if (binsPerArrayDimension is int bins)
        {
            args.Add("--bins-per-array-dimension");
            args.Add(bins.ToString());
        }

        return args;
    }

    private static ProcessStartInfo BuildStartInfo(
        BackendExecutable executable,
        string jsonIn,
        string? outDir,
        string modelDir,
        string workingDirectory,
        int? binsPerArrayDimension)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable.FileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        // ArgumentList, never a concatenated command string - this is the
        // exact injection-prone pattern (Form1.cs's cmd.exe /c string build)
        // the S6 contract replaces.
        foreach (string arg in BuildArgumentList(executable, jsonIn, outDir, modelDir, binsPerArrayDimension))
        {
            startInfo.ArgumentList.Add(arg);
        }

        return startInfo;
    }
}
