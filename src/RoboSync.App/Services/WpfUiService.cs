using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace RoboSync.App.Services;

/// <summary>WPF/Win32 implementation of <see cref="IUiService"/>.</summary>
public sealed class WpfUiService : IUiService
{
    public string? PickFolder(string title, string? initialPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void CopyToClipboard(string text) => Clipboard.SetText(text ?? string.Empty);

    public void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            ShowError("Cannot open location", ex.Message);
        }
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
