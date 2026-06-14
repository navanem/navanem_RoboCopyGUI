namespace RoboSync.App.Services;

/// <summary>
/// Thin seam over Windows-specific UI interactions (dialogs, clipboard, shell) so the
/// view model stays free of direct WPF/Win32 calls and remains the single source of logic.
/// </summary>
public interface IUiService
{
    /// <summary>Shows a folder picker and returns the chosen path, or null if cancelled.</summary>
    string? PickFolder(string title, string? initialPath);

    /// <summary>Copies text to the system clipboard.</summary>
    void CopyToClipboard(string text);

    /// <summary>Opens a file or folder in Windows Explorer.</summary>
    void OpenInExplorer(string path);

    /// <summary>Asks the user to confirm a potentially destructive action.</summary>
    bool Confirm(string title, string message);

    /// <summary>Displays an error dialog.</summary>
    void ShowError(string title, string message);
}
