using RoboSync.Core.Models;
using RoboSync.Core.Validation;

namespace RoboSync.Core.Tests;

public class JobValidatorTests
{
    private static JobConfiguration ValidJob() => new()
    {
        Name = "Test",
        SourcePath = @"C:\src",
        DestinationPath = @"C:\dst",
        Mode = OperationMode.Copy,
        Options = new CopyOptions(),
    };

    [Fact]
    public void Valid_job_passes_when_existence_check_disabled()
    {
        var result = JobValidator.Validate(ValidJob(), checkExistence: false);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_source_is_an_error()
    {
        var job = ValidJob();
        job.SourcePath = "";
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_destination_is_an_error()
    {
        var job = ValidJob();
        job.DestinationPath = "";
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("destination", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Identical_source_and_destination_is_an_error()
    {
        var job = ValidJob();
        job.DestinationPath = job.SourcePath;
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Destination_inside_source_is_an_error()
    {
        var job = ValidJob();
        job.DestinationPath = @"C:\src\backup";
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("inside the source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Negative_retry_count_is_an_error()
    {
        var job = ValidJob();
        job.Options.RetryCount = -1;
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Out_of_range_thread_count_is_an_error()
    {
        var job = ValidJob();
        job.Options.MultiThreaded = true;
        job.Options.ThreadCount = 0;
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Mirror_mode_produces_a_deletion_warning()
    {
        var job = ValidJob();
        job.Mode = OperationMode.Mirror;
        var result = JobValidator.Validate(job, checkExistence: false);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("deletes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_source_directory_is_flagged_when_existence_checked()
    {
        var job = ValidJob();
        job.SourcePath = @"C:\this-path-should-not-exist-rsx-12345";
        var result = JobValidator.Validate(job, checkExistence: true);
        Assert.False(result.IsValid);
    }
}
