using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using RoboSync.Core.Logging;
using RoboSync.Core.Models;
using RoboSync.Core.Util;

namespace RoboSync.Core.Engine;

/// <summary>
/// The production copy engine. Wraps the native <c>Robocopy.exe</c>, streams and parses its
/// output for live progress, and maps its exit code to a friendly result. Pause is implemented
/// by suspending the Robocopy process; cancel kills the entire process tree.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RobocopyCopyEngine : ICopyEngine
{
    private readonly object _gate = new();
    private Process? _activeProcess;

    public bool IsRunning { get; private set; }

    public async Task<ScanResult?> ScanAsync(JobConfiguration job, IJobLogger? logger, CancellationToken cancellationToken)
    {
        try
        {
            long files = 0;
            long bytes = 0;
            logger?.Info("Scanning source to estimate totals...");

            await ExecuteAsync(
                job,
                listOnly: true,
                allowPause: false,
                onLine: line =>
                {
                    if (line.Type == RoboLineType.File)
                    {
                        files++;
                        bytes += line.Bytes;
                    }
                },
                logRawOutput: false,
                logger: logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger?.Info($"Scan complete: {files:N0} files, {ByteFormatter.Format(bytes)} to process.");
            return new ScanResult { TotalFiles = files, TotalBytes = bytes };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger?.Warn($"Pre-scan failed ({ex.Message}); progress will be shown without an estimate.");
            return null;
        }
    }

    public async Task<JobResult> RunAsync(
        JobConfiguration job,
        IProgress<JobProgress>? progress,
        IJobLogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (IsRunning)
        {
            throw new InvalidOperationException("A job is already running on this engine instance.");
        }

        IsRunning = true;
        var stopwatch = Stopwatch.StartNew();
        long filesProcessed = 0;
        long bytesCopied = 0;
        var cancelledByUser = false;

        try
        {
            logger?.Info($"Starting '{job.Name}' ({job.Mode}).");
            logger?.Info(RobocopyCommandBuilder.BuildPreview(job));

            var exitCode = await ExecuteAsync(
                job,
                listOnly: false,
                allowPause: true,
                onLine: line =>
                {
                    if (line.Type == RoboLineType.File)
                    {
                        filesProcessed++;
                        bytesCopied += line.Bytes;
                        progress?.Report(new JobProgress
                        {
                            CurrentFile = line.Path,
                            FilesProcessed = filesProcessed,
                            BytesCopied = bytesCopied,
                        });
                    }
                },
                logRawOutput: true,
                logger: logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            var success = RobocopyExitCodes.IsSuccess(exitCode);
            var summary = RobocopyExitCodes.Describe(exitCode);
            logger?.Info(summary);

            return new JobResult
            {
                ExitCode = exitCode,
                Success = success,
                Cancelled = false,
                Summary = summary,
                FilesProcessed = filesProcessed,
                BytesCopied = bytesCopied,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            cancelledByUser = true;
            stopwatch.Stop();
            logger?.Warn("Job cancelled by user.");
            return new JobResult
            {
                ExitCode = -1,
                Success = false,
                Cancelled = true,
                Summary = "Cancelled before completion.",
                FilesProcessed = filesProcessed,
                BytesCopied = bytesCopied,
                Elapsed = stopwatch.Elapsed,
            };
        }
        finally
        {
            _ = cancelledByUser; // documented branch; state already captured above
            lock (_gate)
            {
                _activeProcess = null;
            }

            IsRunning = false;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_activeProcess is { HasExited: false } process)
            {
                ProcessSuspender.Suspend(process);
            }
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_activeProcess is { HasExited: false } process)
            {
                ProcessSuspender.Resume(process);
            }
        }
    }

    private async Task<int> ExecuteAsync(
        JobConfiguration job,
        bool listOnly,
        bool allowPause,
        Action<RoboLine> onLine,
        bool logRawOutput,
        IJobLogger? logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = RobocopyCommandBuilder.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in RobocopyCommandBuilder.BuildArguments(job, listOnly))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            var parsed = RobocopyOutputParser.Parse(e.Data);
            if (logRawOutput && e.Data.Trim().Length > 0)
            {
                logger?.Raw(e.Data);
            }

            onLine(parsed);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                logger?.Error(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start Robocopy.exe.");
        }

        if (allowPause)
        {
            lock (_gate)
            {
                _activeProcess = process;
            }
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Kill the whole tree on cancellation, then await the real termination so we read a code.
        await using (cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    // Resume first so a paused process can actually be killed.
                    ProcessSuspender.Resume(process);
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // The process may have exited between the check and the kill; nothing to do.
            }
        }).ConfigureAwait(false))
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }
}
