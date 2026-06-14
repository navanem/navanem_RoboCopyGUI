using System.Globalization;

namespace RoboSync.Core.Util;

/// <summary>Formats byte counts as compact, human-readable strings (e.g. "1.5 GB").</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 ? "0" : "0.##";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }
}
