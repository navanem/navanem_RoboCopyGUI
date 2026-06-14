using RoboSync.Core.Engine;
using RoboSync.Core.Models;

namespace RoboSync.Core.Tests;

/// <summary>
/// End-to-end tests that drive the real <c>Robocopy.exe</c> against temporary folders.
/// They validate the full pipeline: argument building, process execution, output parsing,
/// progress reporting, and exit-code interpretation.
/// </summary>
[Collection("Robocopy integration")]
public class RobocopyCopyEngineTests : IDisposable
{
    private readonly string _root;
    private readonly string _source;
    private readonly string _destination;

    public RobocopyCopyEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RoboSyncEngineTests", Guid.NewGuid().ToString("N"));
        _source = Path.Combine(_root, "source");
        _destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(Path.Combine(_source, "nested"));

        File.WriteAllText(Path.Combine(_source, "alpha.txt"), new string('a', 1024));
        File.WriteAllText(Path.Combine(_source, "beta.log"), new string('b', 2048));
        File.WriteAllText(Path.Combine(_source, "nested", "gamma.txt"), new string('c', 4096));
    }

    private JobConfiguration Job(OperationMode mode = OperationMode.Copy) => new()
    {
        Name = "Integration",
        SourcePath = _source,
        DestinationPath = _destination,
        Mode = mode,
        Options = new CopyOptions { IncludeSubfolders = true, MultiThreaded = false, RetryCount = 0 },
    };

    [Fact]
    public async Task Copy_transfers_all_files_and_reports_success()
    {
        var engine = new RobocopyCopyEngine();
        var updates = new List<JobProgress>();
        var progress = new Progress<JobProgress>(updates.Add);

        var result = await engine.RunAsync(Job(), progress, logger: null, CancellationToken.None);

        Assert.True(result.Success, result.Summary);
        Assert.False(result.Cancelled);
        Assert.True(File.Exists(Path.Combine(_destination, "alpha.txt")));
        Assert.True(File.Exists(Path.Combine(_destination, "beta.log")));
        Assert.True(File.Exists(Path.Combine(_destination, "nested", "gamma.txt")));
        Assert.Equal(3, result.FilesProcessed);
        Assert.Equal(1024 + 2048 + 4096, result.BytesCopied);
    }

    [Fact]
    public async Task Scan_estimates_totals_without_copying()
    {
        var engine = new RobocopyCopyEngine();

        var scan = await engine.ScanAsync(Job(), logger: null, CancellationToken.None);

        Assert.NotNull(scan);
        Assert.Equal(3, scan!.TotalFiles);
        Assert.Equal(1024 + 2048 + 4096, scan.TotalBytes);
        Assert.False(Directory.Exists(_destination) && Directory.EnumerateFiles(_destination).Any());
    }

    [Fact]
    public async Task DryRun_copies_nothing_but_succeeds()
    {
        var job = Job();
        job.Options.DryRun = true;
        var engine = new RobocopyCopyEngine();

        var result = await engine.RunAsync(job, progress: null, logger: null, CancellationToken.None);

        Assert.True(result.Success, result.Summary);
        var destinationHasFiles = Directory.Exists(_destination) && Directory.EnumerateFiles(_destination, "*", SearchOption.AllDirectories).Any();
        Assert.False(destinationHasFiles);
    }

    [Fact]
    public async Task Exclude_pattern_skips_matching_files()
    {
        var job = Job();
        job.Options.ExcludeFilePatterns = new() { "*.log" };
        var engine = new RobocopyCopyEngine();

        var result = await engine.RunAsync(job, progress: null, logger: null, CancellationToken.None);

        Assert.True(result.Success, result.Summary);
        Assert.True(File.Exists(Path.Combine(_destination, "alpha.txt")));
        Assert.False(File.Exists(Path.Combine(_destination, "beta.log")));
    }

    [Fact]
    public async Task Mirror_removes_destination_only_files()
    {
        Directory.CreateDirectory(_destination);
        File.WriteAllText(Path.Combine(_destination, "stale.txt"), "obsolete");
        var engine = new RobocopyCopyEngine();

        var result = await engine.RunAsync(Job(OperationMode.Mirror), progress: null, logger: null, CancellationToken.None);

        Assert.True(result.Success, result.Summary);
        Assert.False(File.Exists(Path.Combine(_destination, "stale.txt")));
        Assert.True(File.Exists(Path.Combine(_destination, "alpha.txt")));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
