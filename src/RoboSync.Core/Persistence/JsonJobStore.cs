using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RoboSync.Core.Models;

namespace RoboSync.Core.Persistence;

/// <summary>
/// File-backed job store. Each job is a human-readable <c>*.robosync.json</c> document,
/// which makes saved jobs easy to inspect, diff, share, and version-control.
/// </summary>
public sealed class JsonJobStore : IJobStore
{
    public const string FileExtension = ".robosync.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonJobStore(string directoryPath)
    {
        DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    /// <summary>The default per-user store under %APPDATA%\RoboSync\jobs.</summary>
    public static JsonJobStore CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonJobStore(Path.Combine(appData, "RoboSync", "jobs"));
    }

    public IReadOnlyList<JobConfiguration> LoadAll()
    {
        var jobs = new List<JobConfiguration>();
        if (!Directory.Exists(DirectoryPath))
        {
            return jobs;
        }

        foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*" + FileExtension))
        {
            try
            {
                var json = File.ReadAllText(file);
                var job = JsonSerializer.Deserialize<JobConfiguration>(json, SerializerOptions);
                if (job is not null)
                {
                    job.Options ??= new CopyOptions();
                    jobs.Add(job);
                }
            }
            catch (JsonException)
            {
                // Skip malformed files rather than failing the whole load.
            }
        }

        return jobs
            .OrderBy(j => j.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string Save(JobConfiguration job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(job.Name))
        {
            throw new ArgumentException("A job name is required to save.", nameof(job));
        }

        var path = GetPathForName(job.Name);
        var json = JsonSerializer.Serialize(job, SerializerOptions);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public void Delete(string jobName)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            return;
        }

        var path = GetPathForName(jobName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPathForName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
        }

        var safe = builder.ToString();
        if (safe.Length == 0)
        {
            safe = "job";
        }

        return Path.Combine(DirectoryPath, safe + FileExtension);
    }
}
