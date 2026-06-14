using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RoboSync.App.Mvvm;
using RoboSync.App.Services;
using RoboSync.Core.Engine;
using RoboSync.Core.Logging;
using RoboSync.Core.Models;
using RoboSync.Core.Persistence;
using RoboSync.Core.Util;
using RoboSync.Core.Validation;

namespace RoboSync.App.ViewModels;

/// <summary>
/// The single view model behind the main window. Owns the editable job, drives the engine,
/// surfaces live progress and logs, and manages saved jobs. All long-running work happens on
/// background threads; UI updates are marshalled back here.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IUiService _ui;
    private readonly IJobStore _store;
    private readonly string _logDirectory;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _clock;
    private readonly Stopwatch _stopwatch = new();

    private RobocopyCopyEngine? _engine;
    private FileJobLogger? _logger;
    private CancellationTokenSource? _cancellation;
    private ScanResult? _scan;
    private bool _suppressPreviewRefresh;

    public MainViewModel(IUiService ui, IJobStore store, string logDirectory)
    {
        _ui = ui;
        _store = store;
        _logDirectory = logDirectory;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        Modes = new ObservableCollection<OperationMode>(Enum.GetValues<OperationMode>());
        SavedJobs = new ObservableCollection<JobConfiguration>();
        LogEntries = new ObservableCollection<LogEntry>();

        BrowseSourceCommand = new RelayCommand(BrowseSource, () => !IsRunning);
        BrowseDestinationCommand = new RelayCommand(BrowseDestination, () => !IsRunning);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
        PauseResumeCommand = new RelayCommand(TogglePause, () => IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        SaveJobCommand = new RelayCommand(SaveJob, () => !IsRunning);
        LoadJobCommand = new RelayCommand(LoadSelectedJob, () => !IsRunning && SelectedSavedJob is not null);
        DeleteJobCommand = new RelayCommand(DeleteSelectedJob, () => !IsRunning && SelectedSavedJob is not null);
        NewJobCommand = new RelayCommand(NewJob, () => !IsRunning);
        CopyPreviewCommand = new RelayCommand(() => _ui.CopyToClipboard(CommandPreview));
        OpenLogFolderCommand = new RelayCommand(() => _ui.OpenInExplorer(_logDirectory));
        ClearLogCommand = new RelayCommand(() => LogEntries.Clear(), () => LogEntries.Count > 0);

        _clock = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _clock.Tick += (_, _) => UpdateTiming();

        RefreshSavedJobs();
        RefreshPreview();
        StatusMessage = "Ready. Choose source and destination folders to begin.";
    }

    // ---- Job configuration --------------------------------------------------------------

    private string _jobName = "New Job";
    public string JobName
    {
        get => _jobName;
        set => SetProperty(ref _jobName, value);
    }

    private string _sourcePath = string.Empty;
    public string SourcePath
    {
        get => _sourcePath;
        set { if (SetProperty(ref _sourcePath, value)) RefreshPreview(); }
    }

    private string _destinationPath = string.Empty;
    public string DestinationPath
    {
        get => _destinationPath;
        set { if (SetProperty(ref _destinationPath, value)) RefreshPreview(); }
    }

    public ObservableCollection<OperationMode> Modes { get; }

    private OperationMode _selectedMode = OperationMode.Copy;
    public OperationMode SelectedMode
    {
        get => _selectedMode;
        set { if (SetProperty(ref _selectedMode, value)) { RefreshPreview(); OnPropertyChanged(nameof(ModeDescription)); } }
    }

    public string ModeDescription => SelectedMode switch
    {
        OperationMode.Copy => "Add and update files in the destination. Destination-only files are kept.",
        OperationMode.Mirror => "Make the destination identical to the source. Extra destination files are deleted.",
        OperationMode.Move => "Copy to the destination, then remove the copied items from the source.",
        OperationMode.Sync => "Copy new and newer files only. Existing destination files are never downgraded.",
        _ => string.Empty,
    };

    // ---- Advanced options ---------------------------------------------------------------

    private bool _includeSubfolders = true;
    public bool IncludeSubfolders { get => _includeSubfolders; set { if (SetProperty(ref _includeSubfolders, value)) RefreshPreview(); } }

    private int _retryCount = 3;
    public int RetryCount { get => _retryCount; set { if (SetProperty(ref _retryCount, value)) RefreshPreview(); } }

    private int _retryWaitSeconds = 5;
    public int RetryWaitSeconds { get => _retryWaitSeconds; set { if (SetProperty(ref _retryWaitSeconds, value)) RefreshPreview(); } }

    private bool _multiThreaded = true;
    public bool MultiThreaded { get => _multiThreaded; set { if (SetProperty(ref _multiThreaded, value)) RefreshPreview(); } }

    private int _threadCount = 8;
    public int ThreadCount { get => _threadCount; set { if (SetProperty(ref _threadCount, value)) RefreshPreview(); } }

    private bool _preserveTimestamps = true;
    public bool PreserveTimestamps { get => _preserveTimestamps; set { if (SetProperty(ref _preserveTimestamps, value)) RefreshPreview(); } }

    private bool _preservePermissions;
    public bool PreservePermissions { get => _preservePermissions; set { if (SetProperty(ref _preservePermissions, value)) RefreshPreview(); } }

    private bool _skipNewerInDestination;
    public bool SkipNewerInDestination { get => _skipNewerInDestination; set { if (SetProperty(ref _skipNewerInDestination, value)) RefreshPreview(); } }

    private bool _dryRun;
    public bool DryRun { get => _dryRun; set { if (SetProperty(ref _dryRun, value)) RefreshPreview(); } }

    private string _excludeFilePatterns = string.Empty;
    public string ExcludeFilePatterns { get => _excludeFilePatterns; set { if (SetProperty(ref _excludeFilePatterns, value)) RefreshPreview(); } }

    private string _excludeFolderPatterns = string.Empty;
    public string ExcludeFolderPatterns { get => _excludeFolderPatterns; set { if (SetProperty(ref _excludeFolderPatterns, value)) RefreshPreview(); } }

    // ---- Command preview ----------------------------------------------------------------

    private string _commandPreview = string.Empty;
    public string CommandPreview { get => _commandPreview; private set => SetProperty(ref _commandPreview, value); }

    // ---- Progress -----------------------------------------------------------------------

    private string _currentFile = string.Empty;
    public string CurrentFile { get => _currentFile; private set => SetProperty(ref _currentFile, value); }

    private long _filesProcessed;
    public long FilesProcessed { get => _filesProcessed; private set { if (SetProperty(ref _filesProcessed, value)) OnPropertyChanged(nameof(FilesProcessedText)); } }

    public string FilesProcessedText =>
        _scan is { TotalFiles: > 0 } scan ? $"{FilesProcessed:N0} / {scan.TotalFiles:N0}" : $"{FilesProcessed:N0}";

    private long _bytesCopied;
    public long BytesCopied { get => _bytesCopied; private set { if (SetProperty(ref _bytesCopied, value)) OnPropertyChanged(nameof(BytesCopiedText)); } }

    public string BytesCopiedText =>
        _scan is { TotalBytes: > 0 } scan
            ? $"{ByteFormatter.Format(BytesCopied)} / {ByteFormatter.Format(scan.TotalBytes)}"
            : ByteFormatter.Format(BytesCopied);

    private double _progressValue;
    public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }

    private bool _progressIsIndeterminate;
    public bool ProgressIsIndeterminate { get => _progressIsIndeterminate; private set => SetProperty(ref _progressIsIndeterminate, value); }

    private string _elapsedText = "00:00:00";
    public string ElapsedText { get => _elapsedText; private set => SetProperty(ref _elapsedText, value); }

    private string _etaText = "--:--:--";
    public string EtaText { get => _etaText; private set => SetProperty(ref _etaText, value); }

    // ---- Status & state -----------------------------------------------------------------

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _statusIsError;
    public bool StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsConfigurable));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>True when the form can be edited (no job in flight).</summary>
    public bool IsConfigurable => !IsRunning;

    private bool _isPaused;
    public bool IsPaused { get => _isPaused; private set { if (SetProperty(ref _isPaused, value)) OnPropertyChanged(nameof(PauseResumeLabel)); } }

    public string PauseResumeLabel => IsPaused ? "Resume" : "Pause";

    private string _logFilePath = string.Empty;
    public string LogFilePath { get => _logFilePath; private set => SetProperty(ref _logFilePath, value); }

    // ---- Collections --------------------------------------------------------------------

    public ObservableCollection<LogEntry> LogEntries { get; }

    public ObservableCollection<JobConfiguration> SavedJobs { get; }

    private JobConfiguration? _selectedSavedJob;
    public JobConfiguration? SelectedSavedJob
    {
        get => _selectedSavedJob;
        set { if (SetProperty(ref _selectedSavedJob, value)) RaiseCommandStates(); }
    }

    // ---- Commands -----------------------------------------------------------------------

    public RelayCommand BrowseSourceCommand { get; }
    public RelayCommand BrowseDestinationCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand PauseResumeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SaveJobCommand { get; }
    public RelayCommand LoadJobCommand { get; }
    public RelayCommand DeleteJobCommand { get; }
    public RelayCommand NewJobCommand { get; }
    public RelayCommand CopyPreviewCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    // ---- Command implementations --------------------------------------------------------

    private void BrowseSource()
    {
        var chosen = _ui.PickFolder("Select the source folder", SourcePath);
        if (chosen is not null)
        {
            SourcePath = chosen;
        }
    }

    private void BrowseDestination()
    {
        var chosen = _ui.PickFolder("Select the destination folder", DestinationPath);
        if (chosen is not null)
        {
            DestinationPath = chosen;
        }
    }

    private async Task StartAsync()
    {
        var job = BuildJob();
        var validation = JobValidator.Validate(job);
        if (!validation.IsValid)
        {
            SetStatus("Please fix: " + string.Join(" ", validation.Errors), isError: true);
            _ui.ShowError("Cannot start job", string.Join(Environment.NewLine, validation.Errors));
            return;
        }

        // Confirm destructive modes before doing anything.
        if (job.Mode is OperationMode.Mirror or OperationMode.Move && !job.Options.DryRun)
        {
            var warning = string.Join(Environment.NewLine, validation.Warnings);
            if (!_ui.Confirm($"Confirm {job.Mode}", warning + Environment.NewLine + Environment.NewLine + "Continue?"))
            {
                SetStatus("Job cancelled before start.", isError: false);
                return;
            }
        }

        ResetProgress();
        IsRunning = true;
        IsPaused = false;

        _cancellation = new CancellationTokenSource();
        _engine = new RobocopyCopyEngine();
        _logger = FileJobLogger.CreateForJob(_logDirectory, job.Name, DateTimeOffset.Now);
        _logger.EntryWritten += OnLogEntry;
        LogFilePath = _logger.FilePath ?? string.Empty;

        foreach (var warning in validation.Warnings)
        {
            _logger.Warn(warning);
        }

        _stopwatch.Restart();
        _clock.Start();

        try
        {
            if (!job.Options.DryRun)
            {
                _scan = await _engine.ScanAsync(job, _logger, _cancellation.Token);
                RefreshProgressTotals();
            }

            var progress = new Progress<JobProgress>(OnProgress);
            var result = await _engine.RunAsync(job, progress, _logger, _cancellation.Token);
            FinalizeRun(result);
        }
        catch (Exception ex)
        {
            _logger?.Error("Unexpected error: " + ex.Message);
            SetStatus("Job failed: " + ex.Message, isError: true);
        }
        finally
        {
            _stopwatch.Stop();
            _clock.Stop();
            UpdateTiming();
            if (_logger is not null)
            {
                _logger.EntryWritten -= OnLogEntry;
                _logger.Dispose();
                _logger = null;
            }

            _cancellation?.Dispose();
            _cancellation = null;
            _engine = null;
            IsRunning = false;
            IsPaused = false;
        }
    }

    private void TogglePause()
    {
        if (_engine is null)
        {
            return;
        }

        if (IsPaused)
        {
            _engine.Resume();
            _stopwatch.Start();
            _clock.Start();
            IsPaused = false;
            SetStatus("Resumed.", isError: false);
        }
        else
        {
            _engine.Pause();
            _stopwatch.Stop();
            _clock.Stop();
            IsPaused = true;
            SetStatus("Paused.", isError: false);
        }
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        SetStatus("Cancelling...", isError: false);
    }

    private void SaveJob()
    {
        var job = BuildJob();
        var validation = JobValidator.Validate(job, checkExistence: false);
        if (string.IsNullOrWhiteSpace(job.Name))
        {
            _ui.ShowError("Cannot save", "Enter a job name first.");
            return;
        }

        try
        {
            _store.Save(job);
            RefreshSavedJobs();
            SelectedSavedJob = SavedJobs.FirstOrDefault(j => j.Name == job.Name);
            SetStatus($"Saved job '{job.Name}'.", isError: false);
        }
        catch (Exception ex)
        {
            _ui.ShowError("Cannot save job", ex.Message);
        }
    }

    private void LoadSelectedJob()
    {
        if (SelectedSavedJob is null)
        {
            return;
        }

        ApplyJob(SelectedSavedJob);
        SetStatus($"Loaded job '{SelectedSavedJob.Name}'.", isError: false);
    }

    private void DeleteSelectedJob()
    {
        if (SelectedSavedJob is null)
        {
            return;
        }

        var name = SelectedSavedJob.Name;
        if (!_ui.Confirm("Delete job", $"Delete the saved job '{name}'?"))
        {
            return;
        }

        _store.Delete(name);
        RefreshSavedJobs();
        SetStatus($"Deleted job '{name}'.", isError: false);
    }

    private void NewJob()
    {
        ApplyJob(new JobConfiguration());
        SetStatus("Started a new job.", isError: false);
    }

    // ---- Engine callbacks ---------------------------------------------------------------

    private void OnProgress(JobProgress progress)
    {
        FilesProcessed = progress.FilesProcessed;
        BytesCopied = progress.BytesCopied;
        if (!string.IsNullOrEmpty(progress.CurrentFile))
        {
            CurrentFile = progress.CurrentFile;
        }

        if (_scan is { TotalBytes: > 0 } scan)
        {
            ProgressValue = Math.Clamp(BytesCopied / (double)scan.TotalBytes * 100.0, 0, 100);
        }
    }

    private void OnLogEntry(object? sender, LogEntry entry)
    {
        // Engine raises this from a background thread; marshal to the UI thread.
        _dispatcher.InvokeAsync(() =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 5000)
            {
                LogEntries.RemoveAt(0);
            }

            ClearLogCommand.RaiseCanExecuteChanged();
        });
    }

    // ---- Helpers ------------------------------------------------------------------------

    private void FinalizeRun(JobResult result)
    {
        if (result.Success)
        {
            ProgressValue = 100;
            ProgressIsIndeterminate = false;
        }

        FilesProcessed = result.FilesProcessed;
        BytesCopied = result.BytesCopied;
        SetStatus(result.Summary, isError: !result.Success && !result.Cancelled);
    }

    private void UpdateTiming()
    {
        var elapsed = _stopwatch.Elapsed;
        ElapsedText = FormatDuration(elapsed);

        if (_scan is { TotalBytes: > 0 } scan && BytesCopied > 0 && elapsed.TotalSeconds > 1)
        {
            var rate = BytesCopied / elapsed.TotalSeconds; // bytes per second
            var remainingBytes = Math.Max(0, scan.TotalBytes - BytesCopied);
            var eta = TimeSpan.FromSeconds(remainingBytes / Math.Max(rate, 1));
            EtaText = FormatDuration(eta);
        }
        else
        {
            EtaText = "--:--:--";
        }
    }

    private void RefreshProgressTotals()
    {
        ProgressIsIndeterminate = _scan is null || _scan.TotalBytes <= 0;
        OnPropertyChanged(nameof(FilesProcessedText));
        OnPropertyChanged(nameof(BytesCopiedText));
    }

    private void ResetProgress()
    {
        _scan = null;
        FilesProcessed = 0;
        BytesCopied = 0;
        CurrentFile = string.Empty;
        ProgressValue = 0;
        ProgressIsIndeterminate = true;
        ElapsedText = "00:00:00";
        EtaText = "--:--:--";
        OnPropertyChanged(nameof(FilesProcessedText));
        OnPropertyChanged(nameof(BytesCopiedText));
    }

    private JobConfiguration BuildJob() => new()
    {
        Name = JobName.Trim(),
        SourcePath = SourcePath.Trim(),
        DestinationPath = DestinationPath.Trim(),
        Mode = SelectedMode,
        Options = new CopyOptions
        {
            IncludeSubfolders = IncludeSubfolders,
            RetryCount = RetryCount,
            RetryWaitSeconds = RetryWaitSeconds,
            MultiThreaded = MultiThreaded,
            ThreadCount = ThreadCount,
            PreserveTimestamps = PreserveTimestamps,
            PreservePermissions = PreservePermissions,
            SkipNewerInDestination = SkipNewerInDestination,
            DryRun = DryRun,
            ExcludeFilePatterns = ParsePatterns(ExcludeFilePatterns),
            ExcludeFolderPatterns = ParsePatterns(ExcludeFolderPatterns),
        },
    };

    private void ApplyJob(JobConfiguration job)
    {
        _suppressPreviewRefresh = true;
        try
        {
            JobName = job.Name;
            SourcePath = job.SourcePath;
            DestinationPath = job.DestinationPath;
            SelectedMode = job.Mode;
            var o = job.Options ?? new CopyOptions();
            IncludeSubfolders = o.IncludeSubfolders;
            RetryCount = o.RetryCount;
            RetryWaitSeconds = o.RetryWaitSeconds;
            MultiThreaded = o.MultiThreaded;
            ThreadCount = o.ThreadCount;
            PreserveTimestamps = o.PreserveTimestamps;
            PreservePermissions = o.PreservePermissions;
            SkipNewerInDestination = o.SkipNewerInDestination;
            DryRun = o.DryRun;
            ExcludeFilePatterns = string.Join(Environment.NewLine, o.ExcludeFilePatterns);
            ExcludeFolderPatterns = string.Join(Environment.NewLine, o.ExcludeFolderPatterns);
        }
        finally
        {
            _suppressPreviewRefresh = false;
            RefreshPreview();
        }
    }

    private void RefreshSavedJobs()
    {
        SavedJobs.Clear();
        foreach (var job in _store.LoadAll())
        {
            SavedJobs.Add(job);
        }
    }

    private void RefreshPreview()
    {
        if (_suppressPreviewRefresh)
        {
            return;
        }

        CommandPreview = RobocopyCommandBuilder.BuildPreview(BuildJob());
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }

    private void RaiseCommandStates()
    {
        BrowseSourceCommand.RaiseCanExecuteChanged();
        BrowseDestinationCommand.RaiseCanExecuteChanged();
        StartCommand.RaiseCanExecuteChanged();
        PauseResumeCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        SaveJobCommand.RaiseCanExecuteChanged();
        LoadJobCommand.RaiseCanExecuteChanged();
        DeleteJobCommand.RaiseCanExecuteChanged();
        NewJobCommand.RaiseCanExecuteChanged();
    }

    private static List<string> ParsePatterns(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return text
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"00:{value.Minutes:00}:{value.Seconds:00}";
}
