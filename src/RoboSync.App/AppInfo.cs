using System.Reflection;

namespace RoboSync.App;

/// <summary>
/// Central, read-only source of product metadata used by the header and the About window.
/// The version is read from the assembly so it always matches the build.
/// </summary>
public static class AppInfo
{
    public const string Name = "RoboSync";
    public const string Vendor = "navanem.com";
    public const string Website = "https://www.navanem.com";

    public const string Description =
        "A friendly front-end for Windows Robocopy. Copy, mirror, move, and sync your " +
        "folders with a clean, modern interface — no command line required.";

    /// <summary>The product version, e.g. "1.0.0".</summary>
    public static string Version { get; } = ResolveVersion();

    /// <summary>Display string for the header/About, e.g. "Version 1.0.0".</summary>
    public static string VersionDisplay => "Version " + Version;

    /// <summary>Small attribution shown next to the product name, e.g. "by navanem.com".</summary>
    public static string VendorLine => "by " + Vendor;

    /// <summary>Copyright line for the About window.</summary>
    public static string Copyright => "Copyright © 2026 " + Vendor;

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // InformationalVersion is generated from <Version> and works in single-file builds
        // (unlike Assembly.Location, which is empty when the app is bundled).
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip any source-revision suffix such as "1.0.0+abc123".
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
