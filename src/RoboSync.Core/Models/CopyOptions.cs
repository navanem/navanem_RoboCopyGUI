namespace RoboSync.Core.Models;

/// <summary>
/// Advanced options that fine-tune a copy job. Defaults are chosen to be safe and
/// sensible for typical backup / sync workflows.
/// </summary>
public sealed class CopyOptions
{
    /// <summary>Recurse into subdirectories (Robocopy <c>/E</c>). Defaults to enabled.</summary>
    public bool IncludeSubfolders { get; set; } = true;

    /// <summary>Number of retries on a failed copy (Robocopy <c>/R:n</c>).</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Seconds to wait between retries (Robocopy <c>/W:n</c>).</summary>
    public int RetryWaitSeconds { get; set; } = 5;

    /// <summary>Use multithreaded copying (Robocopy <c>/MT:n</c>).</summary>
    public bool MultiThreaded { get; set; } = true;

    /// <summary>Number of copy threads when <see cref="MultiThreaded"/> is enabled (1-128).</summary>
    public int ThreadCount { get; set; } = 8;

    /// <summary>Preserve directory timestamps in addition to the file timestamps Robocopy keeps by default (<c>/DCOPY:DAT</c>).</summary>
    public bool PreserveTimestamps { get; set; } = true;

    /// <summary>Preserve NTFS security (ACLs) when possible (<c>/COPY:DATS</c>). May require elevation.</summary>
    public bool PreservePermissions { get; set; }

    /// <summary>File name patterns to exclude (Robocopy <c>/XF</c>), for example <c>*.tmp</c> or <c>Thumbs.db</c>.</summary>
    public List<string> ExcludeFilePatterns { get; set; } = new();

    /// <summary>Folder name patterns to exclude (Robocopy <c>/XD</c>), for example <c>node_modules</c> or <c>.git</c>.</summary>
    public List<string> ExcludeFolderPatterns { get; set; } = new();

    /// <summary>Do not overwrite destination files that are newer than the source (Robocopy <c>/XN</c>).</summary>
    public bool SkipNewerInDestination { get; set; }

    /// <summary>Preview only: list what would change without copying anything (Robocopy <c>/L</c>).</summary>
    public bool DryRun { get; set; }

    /// <summary>Creates a deep copy so the UI can edit a working copy without mutating a saved job.</summary>
    public CopyOptions Clone() => new()
    {
        IncludeSubfolders = IncludeSubfolders,
        RetryCount = RetryCount,
        RetryWaitSeconds = RetryWaitSeconds,
        MultiThreaded = MultiThreaded,
        ThreadCount = ThreadCount,
        PreserveTimestamps = PreserveTimestamps,
        PreservePermissions = PreservePermissions,
        ExcludeFilePatterns = new List<string>(ExcludeFilePatterns),
        ExcludeFolderPatterns = new List<string>(ExcludeFolderPatterns),
        SkipNewerInDestination = SkipNewerInDestination,
        DryRun = DryRun,
    };
}
