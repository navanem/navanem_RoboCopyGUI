using RoboSync.Core.Models;

namespace RoboSync.Core.Validation;

/// <summary>Outcome of validating a job: hard errors block a run; warnings are advisory.</summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public static ValidationResult Valid(IReadOnlyList<string> warnings) =>
        new(true, Array.Empty<string>(), warnings);
}

/// <summary>
/// Pure, side-effect-light validation of a job before it runs. Existence checks can be
/// disabled for unit tests. Destructive modes (Mirror, Move) produce warnings, not errors,
/// because they are legitimate but worth confirming.
/// </summary>
public static class JobValidator
{
    public static ValidationResult Validate(JobConfiguration job, bool checkExistence = true)
    {
        ArgumentNullException.ThrowIfNull(job);
        var errors = new List<string>();
        var warnings = new List<string>();
        var options = job.Options ?? new CopyOptions();

        var source = (job.SourcePath ?? string.Empty).Trim();
        var destination = (job.DestinationPath ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(source))
        {
            errors.Add("Select a source folder.");
        }
        else if (checkExistence && !Directory.Exists(source))
        {
            errors.Add("The source folder does not exist.");
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            errors.Add("Select a destination folder.");
        }

        if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(destination))
        {
            if (PathsEqual(source, destination))
            {
                errors.Add("Source and destination must be different folders.");
            }
            else if (IsSubPath(source, destination))
            {
                errors.Add("The destination cannot be inside the source folder.");
            }
            else if (IsSubPath(destination, source) && options.IncludeSubfolders)
            {
                errors.Add("The source cannot be inside the destination folder when including subfolders.");
            }
        }

        if (options.RetryCount < 0)
        {
            errors.Add("Retry count cannot be negative.");
        }

        if (options.RetryWaitSeconds < 0)
        {
            errors.Add("Retry wait time cannot be negative.");
        }

        if (options.MultiThreaded && (options.ThreadCount < 1 || options.ThreadCount > 128))
        {
            errors.Add("Thread count must be between 1 and 128.");
        }

        switch (job.Mode)
        {
            case OperationMode.Mirror:
                warnings.Add("Mirror deletes files in the destination that are not in the source.");
                break;
            case OperationMode.Move:
                warnings.Add("Move deletes the copied files from the source after a successful copy.");
                break;
        }

        if (options.PreservePermissions)
        {
            warnings.Add("Preserving NTFS permissions may require running as administrator.");
        }

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsSubPath(string parent, string child)
    {
        var normalizedParent = Normalize(parent) + Path.DirectorySeparatorChar;
        var normalizedChild = Normalize(child) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        var trimmed = path.Trim().Trim('"');
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception)
        {
            // Fall back to a lexical normalization if the path is syntactically invalid.
            return trimmed.TrimEnd('\\', '/');
        }
    }
}
