using System.Text;
using RoboSync.Core.Models;

namespace RoboSync.Core.Engine;

/// <summary>
/// Translates a <see cref="JobConfiguration"/> into a precise Robocopy argument list.
/// The same builder feeds both the live process and the on-screen command preview,
/// so what the user sees is exactly what runs.
/// </summary>
public static class RobocopyCommandBuilder
{
    /// <summary>The Robocopy executable. It ships with every supported version of Windows.</summary>
    public const string Executable = "Robocopy.exe";

    /// <summary>
    /// Builds the ordered list of arguments (without shell quoting) ready for
    /// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>.
    /// </summary>
    /// <param name="job">The job to translate.</param>
    /// <param name="listOnly">
    /// When true, forces <c>/L</c> (list only). Used for the pre-scan and honored automatically
    /// when <see cref="CopyOptions.DryRun"/> is set.
    /// </param>
    public static IReadOnlyList<string> BuildArguments(JobConfiguration job, bool listOnly = false)
    {
        ArgumentNullException.ThrowIfNull(job);
        var options = job.Options ?? new CopyOptions();
        var args = new List<string>
        {
            NormalizeFolder(job.SourcePath),
            NormalizeFolder(job.DestinationPath),
        };

        // --- Operation mode -> structural switches -------------------------------------
        switch (job.Mode)
        {
            case OperationMode.Copy:
                if (options.IncludeSubfolders) args.Add("/E");
                break;

            case OperationMode.Mirror:
                // /MIR implies recursion and purges destination-only files.
                args.Add("/MIR");
                break;

            case OperationMode.Move:
                if (options.IncludeSubfolders) args.Add("/E");
                args.Add("/MOVE"); // move files AND directories (delete from source after copy)
                break;

            case OperationMode.Sync:
                if (options.IncludeSubfolders) args.Add("/E");
                args.Add("/XO"); // exclude older: only new and newer files flow to the destination
                break;
        }

        // --- Reliability ---------------------------------------------------------------
        // Always set explicit retry/wait: Robocopy's default is 1,000,000 retries, which can hang.
        args.Add($"/R:{Math.Max(0, options.RetryCount)}");
        args.Add($"/W:{Math.Max(0, options.RetryWaitSeconds)}");

        if (options.MultiThreaded)
        {
            var threads = Math.Clamp(options.ThreadCount, 1, 128);
            args.Add($"/MT:{threads}");
        }

        // --- Fidelity ------------------------------------------------------------------
        if (options.PreservePermissions)
        {
            // D=Data, A=Attributes, T=Timestamps, S=NTFS security/ACLs.
            args.Add("/COPY:DATS");
        }

        if (options.PreserveTimestamps)
        {
            // Files keep their timestamps by default; this also carries directory timestamps.
            args.Add("/DCOPY:DAT");
        }

        if (options.SkipNewerInDestination)
        {
            args.Add("/XN"); // exclude newer: never overwrite a destination file that is newer
        }

        // --- Exclusions ----------------------------------------------------------------
        var excludeFiles = CleanPatterns(options.ExcludeFilePatterns);
        if (excludeFiles.Count > 0)
        {
            args.Add("/XF");
            args.AddRange(excludeFiles);
        }

        var excludeFolders = CleanPatterns(options.ExcludeFolderPatterns);
        if (excludeFolders.Count > 0)
        {
            args.Add("/XD");
            args.AddRange(excludeFolders);
        }

        // --- Output formatting (machine-friendly for parsing) --------------------------
        args.Add("/BYTES"); // exact byte counts instead of human-rounded sizes
        args.Add("/FP");    // full path names so the "current file" reads clearly
        args.Add("/NP");    // no per-file percentage spam (keeps stdout line-oriented)

        if (listOnly || options.DryRun)
        {
            args.Add("/L"); // list only, copy nothing
        }

        return args;
    }

    /// <summary>Builds a copy/paste-ready command string for the preview pane.</summary>
    public static string BuildPreview(JobConfiguration job, bool listOnly = false)
    {
        var sb = new StringBuilder(Executable);
        foreach (var arg in BuildArguments(job, listOnly))
        {
            sb.Append(' ').Append(Quote(arg));
        }

        return sb.ToString();
    }

    private static List<string> CleanPatterns(IEnumerable<string>? patterns) =>
        patterns is null
            ? new List<string>()
            : patterns.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

    private static string NormalizeFolder(string path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('"');
        // A trailing backslash before a closing quote confuses command-line parsing
        // (the backslash escapes the quote). Drop it, except for drive roots like "C:\".
        if (trimmed.Length > 3 && trimmed.EndsWith('\\'))
        {
            trimmed = trimmed.TrimEnd('\\');
        }

        return trimmed;
    }

    private static string Quote(string arg) =>
        arg.Contains(' ') || arg.Contains('\t') ? $"\"{arg}\"" : arg;
}
