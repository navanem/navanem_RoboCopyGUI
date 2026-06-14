using System.Globalization;
using System.Text;

namespace RoboSync.Core.Logging;

/// <summary>
/// Logger that writes timestamped entries to a UTF-8 log file and simultaneously raises
/// <see cref="EntryWritten"/> for the live UI panel. Thread-safe: the engine writes from
/// background threads while the UI reads via the event.
/// </summary>
public sealed class FileJobLogger : IJobLogger
{
    private readonly object _writeLock = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public FileJobLogger(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(filePath, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public string? FilePath { get; }

    public event EventHandler<LogEntry>? EntryWritten;

    /// <summary>Creates a logger in the given directory with a timestamped, job-named file.</summary>
    public static FileJobLogger CreateForJob(string logDirectory, string jobName, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(logDirectory);
        var safeName = MakeSafeFileName(jobName);
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(logDirectory, $"{stamp}_{safeName}.log");
        return new FileJobLogger(path);
    }

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warn(string message) => Write(LogLevel.Warning, message);

    public void Error(string message) => Write(LogLevel.Error, message);

    public void Raw(string line) => Write(LogLevel.Raw, line);

    private void Write(LogLevel level, string message)
    {
        if (_disposed)
        {
            return;
        }

        var entry = new LogEntry(DateTimeOffset.Now, level, message);
        var line = Format(entry);

        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine(line);
        }

        EntryWritten?.Invoke(this, entry);
    }

    private static string Format(LogEntry entry)
    {
        var time = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return entry.Level == LogLevel.Raw
            ? $"{time}      {entry.Message}"
            : $"{time} [{entry.Level.ToString().ToUpperInvariant()}] {entry.Message}";
    }

    private static string MakeSafeFileName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? "job" : result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
