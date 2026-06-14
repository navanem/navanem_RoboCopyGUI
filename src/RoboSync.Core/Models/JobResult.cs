namespace RoboSync.Core.Models;

/// <summary>
/// Final outcome of a job run, including the raw Robocopy exit code and a human-readable summary.
/// </summary>
public sealed class JobResult
{
    /// <summary>The raw process exit code returned by Robocopy.</summary>
    public int ExitCode { get; init; }

    /// <summary>True when the run completed without errors (Robocopy exit codes 0-7).</summary>
    public bool Success { get; init; }

    /// <summary>True when the user cancelled the run before it finished.</summary>
    public bool Cancelled { get; init; }

    /// <summary>Human-readable description of the exit code.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Files copied over the whole run.</summary>
    public long FilesProcessed { get; init; }

    /// <summary>Total bytes copied over the whole run.</summary>
    public long BytesCopied { get; init; }

    /// <summary>Wall-clock duration of the run.</summary>
    public TimeSpan Elapsed { get; init; }
}
