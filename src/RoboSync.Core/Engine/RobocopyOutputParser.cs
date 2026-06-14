using System.Globalization;

namespace RoboSync.Core.Engine;

/// <summary>The kind of information a single line of Robocopy output carries.</summary>
public enum RoboLineType
{
    /// <summary>A file that was (or, in list mode, would be) copied. Carries a byte size and path.</summary>
    File,

    /// <summary>A directory header line. Useful context but not counted toward file progress.</summary>
    Directory,

    /// <summary>A line from the final summary block (totals).</summary>
    Summary,

    /// <summary>An error line (for example "ERROR 5 (0x...) Accessing Source Directory").</summary>
    Error,

    /// <summary>Anything else: banners, option echoes, blank lines.</summary>
    Other,
}

/// <summary>A structured view of one line of Robocopy output.</summary>
public sealed record RoboLine(RoboLineType Type, string Raw, string? Path = null, long Bytes = 0, string? Class = null);

/// <summary>
/// Parses Robocopy's line-oriented, tab-delimited output (as produced with the
/// <c>/BYTES /FP /NP</c> switches the builder always adds). Kept deliberately tolerant:
/// unknown lines degrade to <see cref="RoboLineType.Other"/> rather than throwing.
/// </summary>
public static class RobocopyOutputParser
{
    public static RoboLine Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new RoboLine(RoboLineType.Other, line ?? string.Empty);
        }

        var raw = line;

        // Robocopy error lines carry the pattern "ERROR n (0xXXXXXXXX) ...", usually prefixed
        // with a timestamp, so match the signature anywhere in the line rather than at the start.
        if (raw.TrimStart().StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
            (raw.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
             raw.Contains("(0x", StringComparison.OrdinalIgnoreCase)))
        {
            return new RoboLine(RoboLineType.Error, raw);
        }

        // Tokenize on tabs; Robocopy aligns columns with tabs.
        var tokens = raw.Split('\t')
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToArray();

        if (tokens.Length == 0)
        {
            return Classify(raw);
        }

        var first = tokens[0];

        // "*EXTRA File" / "*EXTRA Dir" describe destination items being removed (mirror/move),
        // not bytes we are copying. Treat them as non-counting context lines.
        if (first.StartsWith('*'))
        {
            return new RoboLine(RoboLineType.Other, raw, Path: tokens[^1]);
        }

        // Directory header lines, e.g. "New Dir          12   C:\path\".
        if (first.Contains("Dir", StringComparison.OrdinalIgnoreCase))
        {
            return new RoboLine(RoboLineType.Directory, raw, Path: tokens[^1]);
        }

        // A file line has a pure-integer size token and a path as the final token.
        for (var i = 0; i < tokens.Length; i++)
        {
            if (long.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            {
                var path = tokens[^1];
                if (i == tokens.Length - 1)
                {
                    // The size was the last token; no path followed -> not a file entry.
                    break;
                }

                var cls = i > 0 ? tokens[0] : null;
                return new RoboLine(RoboLineType.File, raw, Path: path, Bytes: size, Class: cls);
            }
        }

        return Classify(raw);
    }

    private static RoboLine Classify(string raw)
    {
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith("Total", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Files :", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Bytes :", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Dirs :", StringComparison.OrdinalIgnoreCase))
        {
            return new RoboLine(RoboLineType.Summary, raw);
        }

        return new RoboLine(RoboLineType.Other, raw);
    }
}
