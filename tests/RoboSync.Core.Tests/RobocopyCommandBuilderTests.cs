using RoboSync.Core.Engine;
using RoboSync.Core.Models;

namespace RoboSync.Core.Tests;

public class RobocopyCommandBuilderTests
{
    private static JobConfiguration BaseJob() => new()
    {
        Name = "Test",
        SourcePath = @"C:\src",
        DestinationPath = @"C:\dst",
        Mode = OperationMode.Copy,
        Options = new CopyOptions
        {
            IncludeSubfolders = true,
            RetryCount = 3,
            RetryWaitSeconds = 5,
            MultiThreaded = true,
            ThreadCount = 8,
            PreserveTimestamps = true,
        },
    };

    [Fact]
    public void Copy_with_subfolders_uses_E()
    {
        var args = RobocopyCommandBuilder.BuildArguments(BaseJob());
        Assert.Equal(@"C:\src", args[0]);
        Assert.Equal(@"C:\dst", args[1]);
        Assert.Contains("/E", args);
        Assert.DoesNotContain("/MIR", args);
    }

    [Fact]
    public void Copy_without_subfolders_omits_E()
    {
        var job = BaseJob();
        job.Options.IncludeSubfolders = false;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.DoesNotContain("/E", args);
    }

    [Fact]
    public void Mirror_uses_MIR_and_not_E()
    {
        var job = BaseJob();
        job.Mode = OperationMode.Mirror;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/MIR", args);
        Assert.DoesNotContain("/E", args);
    }

    [Fact]
    public void Move_adds_MOVE()
    {
        var job = BaseJob();
        job.Mode = OperationMode.Move;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/MOVE", args);
    }

    [Fact]
    public void Sync_excludes_older_with_XO()
    {
        var job = BaseJob();
        job.Mode = OperationMode.Sync;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/XO", args);
    }

    [Fact]
    public void Retry_and_wait_are_always_explicit()
    {
        var args = RobocopyCommandBuilder.BuildArguments(BaseJob());
        Assert.Contains("/R:3", args);
        Assert.Contains("/W:5", args);
    }

    [Fact]
    public void Multithreading_emits_clamped_thread_count()
    {
        var job = BaseJob();
        job.Options.ThreadCount = 999;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/MT:128", args);
    }

    [Fact]
    public void Permissions_use_COPY_DATS()
    {
        var job = BaseJob();
        job.Options.PreservePermissions = true;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/COPY:DATS", args);
    }

    [Fact]
    public void Exclusions_are_grouped_after_their_switch()
    {
        var job = BaseJob();
        job.Options.ExcludeFilePatterns = new() { "*.tmp", "Thumbs.db" };
        job.Options.ExcludeFolderPatterns = new() { "node_modules" };
        var args = RobocopyCommandBuilder.BuildArguments(job).ToList();

        var xf = args.IndexOf("/XF");
        Assert.True(xf >= 0);
        Assert.Equal("*.tmp", args[xf + 1]);
        Assert.Equal("Thumbs.db", args[xf + 2]);

        var xd = args.IndexOf("/XD");
        Assert.True(xd >= 0);
        Assert.Equal("node_modules", args[xd + 1]);
    }

    [Fact]
    public void DryRun_forces_list_only()
    {
        var job = BaseJob();
        job.Options.DryRun = true;
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Contains("/L", args);
    }

    [Fact]
    public void ListOnly_flag_forces_L_even_without_dry_run()
    {
        var args = RobocopyCommandBuilder.BuildArguments(BaseJob(), listOnly: true);
        Assert.Contains("/L", args);
    }

    [Fact]
    public void Preview_quotes_paths_with_spaces()
    {
        var job = BaseJob();
        job.SourcePath = @"C:\My Files";
        var preview = RobocopyCommandBuilder.BuildPreview(job);
        Assert.StartsWith("Robocopy.exe \"C:\\My Files\"", preview);
    }

    [Fact]
    public void Trailing_backslash_is_trimmed_to_avoid_quote_escaping()
    {
        var job = BaseJob();
        job.SourcePath = @"C:\My Files\";
        var args = RobocopyCommandBuilder.BuildArguments(job);
        Assert.Equal(@"C:\My Files", args[0]);
    }
}
