using RoboSync.Core.Models;
using RoboSync.Core.Persistence;

namespace RoboSync.Core.Tests;

public class JsonJobStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonJobStore _store;

    public JsonJobStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RoboSyncTests", Guid.NewGuid().ToString("N"));
        _store = new JsonJobStore(_dir);
    }

    private static JobConfiguration SampleJob(string name = "My Backup") => new()
    {
        Name = name,
        SourcePath = @"C:\src",
        DestinationPath = @"D:\backup",
        Mode = OperationMode.Mirror,
        Options = new CopyOptions
        {
            RetryCount = 7,
            ExcludeFilePatterns = new() { "*.tmp" },
            ExcludeFolderPatterns = new() { ".git" },
        },
    };

    [Fact]
    public void Save_then_load_round_trips_all_fields()
    {
        _store.Save(SampleJob());

        var loaded = _store.LoadAll().Single();
        Assert.Equal("My Backup", loaded.Name);
        Assert.Equal(OperationMode.Mirror, loaded.Mode);
        Assert.Equal(7, loaded.Options.RetryCount);
        Assert.Equal(new[] { "*.tmp" }, loaded.Options.ExcludeFilePatterns);
        Assert.Equal(new[] { ".git" }, loaded.Options.ExcludeFolderPatterns);
    }

    [Fact]
    public void Enum_is_persisted_as_readable_string()
    {
        var path = _store.Save(SampleJob());
        var json = File.ReadAllText(path);
        Assert.Contains("\"Mirror\"", json);
    }

    [Fact]
    public void Saving_same_name_overwrites_rather_than_duplicates()
    {
        _store.Save(SampleJob());
        var updated = SampleJob();
        updated.Options.RetryCount = 99;
        _store.Save(updated);

        var all = _store.LoadAll();
        Assert.Single(all);
        Assert.Equal(99, all[0].Options.RetryCount);
    }

    [Fact]
    public void Delete_removes_the_job()
    {
        _store.Save(SampleJob());
        _store.Delete("My Backup");
        Assert.Empty(_store.LoadAll());
    }

    [Fact]
    public void Names_with_invalid_characters_are_sanitized()
    {
        _store.Save(SampleJob("Backup: C:/Photos*"));
        Assert.Single(_store.LoadAll());
    }

    [Fact]
    public void Malformed_files_are_skipped_not_thrown()
    {
        File.WriteAllText(Path.Combine(_dir, "broken" + JsonJobStore.FileExtension), "{ not valid json ");
        _store.Save(SampleJob());
        Assert.Single(_store.LoadAll());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of the temp directory.
        }
    }
}
