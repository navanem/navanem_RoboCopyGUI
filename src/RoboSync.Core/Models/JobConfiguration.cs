namespace RoboSync.Core.Models;

/// <summary>
/// A complete, serializable description of a copy job: what to copy, where, how,
/// and under which name. This is the unit of work persisted to disk and handed to the engine.
/// </summary>
public sealed class JobConfiguration
{
    /// <summary>Human-friendly job name, also used to derive the saved file name.</summary>
    public string Name { get; set; } = "New Job";

    /// <summary>Absolute path of the source folder.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Absolute path of the destination folder.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>The selected operation mode.</summary>
    public OperationMode Mode { get; set; } = OperationMode.Copy;

    /// <summary>Advanced options.</summary>
    public CopyOptions Options { get; set; } = new();

    /// <summary>Creates an independent copy, used by the UI for safe editing.</summary>
    public JobConfiguration Clone() => new()
    {
        Name = Name,
        SourcePath = SourcePath,
        DestinationPath = DestinationPath,
        Mode = Mode,
        Options = Options.Clone(),
    };
}
