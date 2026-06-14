using RoboSync.Core.Logging;
using RoboSync.Core.Models;

namespace RoboSync.Core.Engine;

/// <summary>
/// Abstraction over the copy engine so the UI depends on a contract, not on Robocopy directly.
/// Implementations are single-use per run but expose <see cref="Pause"/>/<see cref="Resume"/>
/// for the lifetime of an in-flight run.
/// </summary>
public interface ICopyEngine
{
    /// <summary>True while a run is in progress.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Runs a list-only pre-scan to estimate how many files and bytes the job will move.
    /// Returns <c>null</c> if the scan fails or is cancelled; callers treat that as "totals unknown".
    /// </summary>
    Task<ScanResult?> ScanAsync(JobConfiguration job, IJobLogger? logger, CancellationToken cancellationToken);

    /// <summary>Executes the job, reporting progress and forwarding output to the logger.</summary>
    Task<JobResult> RunAsync(
        JobConfiguration job,
        IProgress<JobProgress>? progress,
        IJobLogger? logger,
        CancellationToken cancellationToken);

    /// <summary>Suspends the running job, if the engine supports it. Safe to call when idle.</summary>
    void Pause();

    /// <summary>Resumes a paused job.</summary>
    void Resume();
}
