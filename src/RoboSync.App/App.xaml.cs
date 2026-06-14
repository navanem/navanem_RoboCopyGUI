using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using RoboSync.App.Services;
using RoboSync.App.ViewModels;
using RoboSync.Core.Persistence;

namespace RoboSync.App;

/// <summary>
/// Application entry point and composition root. Wires the concrete services together and
/// shows the main window. Centralizing construction here keeps the rest of the app testable.
/// </summary>
public partial class App : Application
{
    private const string AppFolderName = "RoboSync";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootDirectory = Path.Combine(appData, AppFolderName);
        var jobsDirectory = Path.Combine(rootDirectory, "jobs");
        var logsDirectory = Path.Combine(rootDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        var ui = new WpfUiService();
        var store = new JsonJobStore(jobsDirectory);
        var viewModel = new MainViewModel(ui, store, logsDirectory);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:" + Environment.NewLine + Environment.NewLine + e.Exception.Message,
            "RoboSync",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
