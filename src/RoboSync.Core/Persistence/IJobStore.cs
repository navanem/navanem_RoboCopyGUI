using RoboSync.Core.Models;

namespace RoboSync.Core.Persistence;

/// <summary>Persists saved job configurations so users can reload them later.</summary>
public interface IJobStore
{
    /// <summary>The directory backing this store.</summary>
    string DirectoryPath { get; }

    /// <summary>Loads every saved job, skipping any files that fail to parse.</summary>
    IReadOnlyList<JobConfiguration> LoadAll();

    /// <summary>Saves (or overwrites) a job and returns the file path it was written to.</summary>
    string Save(JobConfiguration job);

    /// <summary>Deletes the saved job with the given name, if present.</summary>
    void Delete(string jobName);
}
