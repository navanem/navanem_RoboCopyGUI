namespace RoboSync.Core.Logging;

/// <summary>Severity of a log entry.</summary>
public enum LogLevel
{
    Info,
    Warning,
    Error,
    /// <summary>Raw, unmodified output forwarded from Robocopy.</summary>
    Raw,
}

/// <summary>A single timestamped log entry, surfaced both to the UI and to the log file.</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message);

/// <summary>
/// Sink for job logging. Implementations write to a file and raise <see cref="EntryWritten"/>
/// so the UI can mirror the log live. Disposing flushes and closes the underlying file.
/// </summary>
public interface IJobLogger : IDisposable
{
    /// <summary>Absolute path of the log file, when the logger is file-backed.</summary>
    string? FilePath { get; }

    void Info(string message);
    void Warn(string message);
    void Error(string message);

    /// <summary>Forwards a raw line of engine output.</summary>
    void Raw(string line);

    /// <summary>Raised on every entry. Handlers must marshal to the UI thread themselves.</summary>
    event EventHandler<LogEntry>? EntryWritten;
}
