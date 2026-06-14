namespace RoboSync.Core.Models;

/// <summary>
/// High-level copy operation the user selects. Each mode maps to a specific,
/// well-defined set of Robocopy switches (see <see cref="Engine.RobocopyCommandBuilder"/>).
/// </summary>
public enum OperationMode
{
    /// <summary>Add and update files in the destination. Never deletes destination-only files.</summary>
    Copy,

    /// <summary>Make the destination an exact replica of the source, including deletions.</summary>
    Mirror,

    /// <summary>Copy to the destination, then delete the copied items from the source.</summary>
    Move,

    /// <summary>One-way incremental sync: copy new and newer files only, keep destination extras.</summary>
    Sync,
}
