namespace RoboSync.Core.Engine;

/// <summary>
/// Interprets Robocopy's bit-encoded exit codes. Codes 0-7 indicate success
/// (possibly with notable but non-fatal observations); 8 and above indicate failure.
/// </summary>
public static class RobocopyExitCodes
{
    /// <summary>True when the run completed without copy failures.</summary>
    public static bool IsSuccess(int exitCode) => exitCode >= 0 && exitCode < 8;

    /// <summary>Builds a plain-English description of a Robocopy exit code.</summary>
    public static string Describe(int exitCode)
    {
        if (exitCode < 0)
        {
            return "The job was stopped before Robocopy reported a result.";
        }

        if (exitCode == 0)
        {
            return "No changes were needed; source and destination already match.";
        }

        if (exitCode == 16)
        {
            return "Fatal error. Robocopy could not copy any files (check paths and permissions).";
        }

        var parts = new List<string>();
        if ((exitCode & 1) != 0) parts.Add("files were copied");
        if ((exitCode & 2) != 0) parts.Add("extra files or folders were detected");
        if ((exitCode & 4) != 0) parts.Add("some mismatched files or folders were found");
        if ((exitCode & 8) != 0) parts.Add("some files or folders could not be copied");

        if (parts.Count == 0)
        {
            return $"Robocopy finished with exit code {exitCode}.";
        }

        var summary = string.Join("; ", parts);
        var prefix = IsSuccess(exitCode) ? "Completed: " : "Completed with errors: ";
        return prefix + char.ToUpperInvariant(summary[0]) + summary[1..] + ".";
    }
}
