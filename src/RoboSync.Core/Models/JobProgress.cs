namespace RoboSync.Core.Models;

/// <summary>
/// A point-in-time snapshot of live job progress, pushed from the engine to the UI.
/// Totals (from the optional pre-scan) and timing are combined with these counters
/// by the view model to compute percentage and ETA.
/// </summary>
public sealed class JobProgress
{
    /// <summary>Full path of the file currently being processed, if known.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>Number of files copied so far.</summary>
    public long FilesProcessed { get; init; }

    /// <summary>Total bytes copied so far.</summary>
    public long BytesCopied { get; init; }
}

/// <summary>
/// Result of the optional list-only pre-scan used to estimate totals for the progress bar and ETA.
/// </summary>
public sealed class ScanResult
{
    public long TotalFiles { get; init; }
    public long TotalBytes { get; init; }
}
