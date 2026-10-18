using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeUninstaller.App.Services;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Windows.Execution;

namespace VibeUninstaller.App.ViewModels;

/// <summary>Per-application state inside a batch uninstall run.</summary>
public enum BatchItemState
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>The phase of the batch window.</summary>
public enum BatchState
{
    Confirm,
    Running,
    Review,
    Removing,
    Done,
}

/// <summary>One application in a batch uninstall run.</summary>
public sealed partial class BatchItemViewModel : ObservableObject
{
    public BatchItemViewModel(InstalledApp app)
    {
        App = app;
        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        Installer = InstallerInfoDetector.Detect(app, parsed);
    }

    public InstalledApp App { get; }

    public InstallerInfo Installer { get; }

    public bool CanUseSilent => Installer.SupportsSilent;

    public string DisplayName => App.DisplayName;

    public string Publisher => string.IsNullOrWhiteSpace(App.Publisher) ? "Unknown publisher" : App.Publisher;

    [ObservableProperty]
    private BatchItemState _state = BatchItemState.Queued;

    [ObservableProperty]
    private string _statusText = "Queued";

    [ObservableProperty]
    private int _leftoverCount;

    public string StateLabel => State switch
    {
        BatchItemState.Queued => "Queued",
        BatchItemState.Running => "Running",
        BatchItemState.Completed => "Done",
        BatchItemState.Canceled => "Skipped",
        _ => "Failed",
    };

    partial void OnStateChanged(BatchItemState value) => OnPropertyChanged(nameof(StateLabel));
}

/// <summary>
/// Runs each application's own uninstaller in turn and collects every leftover
/// into one review list. Nothing beyond the vendor uninstallers themselves
/// happens before the user confirms the removal step.
/// </summary>
public sealed partial class BatchUninstallViewModel : ObservableObject
{
    private const int SizeComputationLimit = 400;

    private readonly UninstallService _service = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Dictionary<LeftoverItemViewModel, BatchItemViewModel> _owners = new();

    public BatchUninstallViewModel(IReadOnlyList<InstalledApp> apps)
    {
        Items = new ObservableCollection<BatchItemViewModel>(apps.Select(app => new BatchItemViewModel(app)));
        BackupRegistryKeys = AppSettingsStore.Load().BackupRegistryKeys;
    }

    public ObservableCollection<BatchItemViewModel> Items { get; }

    public ObservableCollection<LeftoverItemViewModel> Leftovers { get; } = new();

    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty]
    private BatchState _state = BatchState.Confirm;

    [ObservableProperty]
    private bool _useSilent = true;

    [ObservableProperty]
    private bool _scanLeftovers = true;

    [ObservableProperty]
    private string _stageText = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _removalSummary = string.Empty;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _sizeComputationRunning;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _selectedBytes;

    /// <summary>
    /// Export registry keys to .reg files before deleting them. On by default
    /// and persisted, because it is the safety net for a wrong cleanup.
    /// </summary>
    [ObservableProperty]
    private bool _backupRegistryKeys = true;

    public string BackupNote => BackupRegistryKeys
        ? "Every registry key is exported to a .reg file in the backup folder before it is deleted."
        : "Registry keys will be deleted without a .reg backup. Files still go to the Recycle Bin.";

    public bool BackupNoteIsWarning => !BackupRegistryKeys;

    partial void OnBackupRegistryKeysChanged(bool value)
    {
        OnPropertyChanged(nameof(BackupNote));
        OnPropertyChanged(nameof(BackupNoteIsWarning));
        AppSettingsStore.Save(AppSettingsStore.Load() with { BackupRegistryKeys = value });
    }

    public string StateName => State.ToString();

    public string HeaderText => $"{Items.Count} application{(Items.Count == 1 ? string.Empty : "s")} selected";

    public string StartButtonText =>
        $"Uninstall {Items.Count} application{(Items.Count == 1 ? string.Empty : "s")}";

    public string SizeComputationText => SizeComputationRunning ? "Computing sizes…" : string.Empty;

    public bool CanClose => State is BatchState.Confirm or BatchState.Review or BatchState.Done;

    /// <summary>True once an uninstaller ran; the caller rescans the library then.</summary>
    public bool SomethingChanged { get; private set; }

    partial void OnStateChanged(BatchState value)
    {
        OnPropertyChanged(nameof(StateName));
        OnPropertyChanged(nameof(CanClose));
    }

    partial void OnSizeComputationRunningChanged(bool value) =>
        OnPropertyChanged(nameof(SizeComputationText));

    public async Task StartAsync()
    {
        if (State != BatchState.Confirm)
        {
            return;
        }

        State = BatchState.Running;
        ResultSummary = string.Empty;
        ErrorText = null;
        CompletedCount = 0;

        var progress = new Progress<UninstallProgress>(update =>
        {
            if (Items.FirstOrDefault(item => item.State == BatchItemState.Running) is { } current)
            {
                current.StatusText = update.Detail ?? UninstallText.DescribeStage(update.Stage);
            }

            StageText = UninstallText.DescribeStage(update.Stage);
            if (!string.IsNullOrWhiteSpace(update.Detail))
            {
                AddLog(update.Detail);
            }
        });

        foreach (var item in Items)
        {
            if (_cancellation.IsCancellationRequested)
            {
                item.State = BatchItemState.Canceled;
                item.StatusText = "Skipped";
                continue;
            }

            item.State = BatchItemState.Running;
            item.StatusText = "Starting…";
            StageText = $"Uninstalling {CompletedCount + 1} of {Items.Count}: {item.DisplayName}";

            try
            {
                var session = await _service
                    .UninstallAsync(item.App, new UninstallOptions
                    {
                        ScanLeftovers = ScanLeftovers,
                        UseSilentUninstall = UseSilent && item.CanUseSilent,
                    }, progress, _cancellation.Token)
                    .ConfigureAwait(true);

                SomethingChanged |= session.Run.Status is UninstallRunStatus.Succeeded or UninstallRunStatus.Failed;

                if (session.Scan is { Items.Count: > 0 } scan)
                {
                    foreach (var leftover in scan.Items)
                    {
                        var viewModel = new LeftoverItemViewModel(leftover);
                        _owners[viewModel] = item;
                        Leftovers.Add(viewModel);
                    }
                }

                item.LeftoverCount = session.Scan?.Items.Count ?? 0;
                item.State = session.Run.Status == UninstallRunStatus.Succeeded
                    ? BatchItemState.Completed
                    : BatchItemState.Failed;
                item.StatusText = BuildItemStatus(session);
            }
            catch (OperationCanceledException)
            {
                item.State = BatchItemState.Canceled;
                item.StatusText = "Canceled";
            }
            catch (Exception exception)
            {
                item.State = BatchItemState.Failed;
                item.StatusText = exception.Message;
                AddLog($"{item.DisplayName}: {exception.Message}");
            }

            CompletedCount++;
        }

        StageText = string.Empty;
        ResultSummary = BuildResultSummary();

        if (Leftovers.Count > 0)
        {
            AttachSelectionTracking();
            UpdateSelectedTotals();
            State = BatchState.Review;
            StartSizeComputation();
        }
        else
        {
            State = BatchState.Done;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (State == BatchState.Running)
        {
            _cancellation.Cancel();
        }
    }

    [RelayCommand]
    private void SelectAll() => SetSelection(_ => true);

    [RelayCommand]
    private void SelectNone() => SetSelection(_ => false);

    [RelayCommand]
    private void SelectRecommended() => SetSelection(item => item.Tier != RiskTier.Advanced);

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var selected = Leftovers.Where(item => item.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            ErrorText = "Select at least one item to remove.";
            return;
        }

        State = BatchState.Removing;
        StageText = "Removing leftovers...";
        ErrorText = null;

        var progress = new Progress<string>(AddLog);
        var executor = new RemovalExecutor();
        var files = 0;
        var bytes = 0L;
        var keys = 0;
        var errors = new List<string>();
        var backupsSkipped = false;
        string? vault = null;

        try
        {
            foreach (var group in selected.GroupBy(item => _owners[item]))
            {
                StageText = $"Removing leftovers of {group.Key.DisplayName}...";
                var items = group.Select(item => item.Item with { SizeBytes = item.SizeBytes }).ToArray();
                var report = await executor
                    .RemoveAsync(
                        group.Key.App,
                        items,
                        progress,
                        CancellationToken.None,
                        new LeftoverRemovalOptions { BackupRegistryKeys = BackupRegistryKeys })
                    .ConfigureAwait(true);

                files += report.FilesRemoved;
                bytes += report.BytesRemoved;
                keys += report.RegistryRemoved;
                backupsSkipped |= report.RegistryBackupSkipped;
                vault ??= report.VaultPath;
                errors.AddRange(report.Errors);
            }

            RemovalSummary =
                $"Recycled {files:N0} file item(s) ({bytes / 1024.0 / 1024.0:N1} MB) · " +
                $"removed {keys:N0} registry key(s) · " +
                (backupsSkipped ? "registry backups were skipped" : $"registry backups in {vault}");

            if (errors.Count > 0)
            {
                ErrorText = string.Join(Environment.NewLine, errors.Take(10));
            }

            SomethingChanged = true;
            State = BatchState.Done;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            State = BatchState.Done;
        }
    }

    [RelayCommand]
    private void OpenVault()
    {
        var vault = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VibeUninstaller",
            "Vault");

        if (!System.IO.Directory.Exists(vault))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{vault}\"")
        {
            UseShellExecute = true,
        });
    }

    private static string BuildItemStatus(UninstallSessionResult session)
    {
        var status = UninstallText.DescribeRun(session.Run);
        return session.Scan is { Items.Count: > 0 } scan
            ? $"{status} · {scan.Items.Count} leftover item(s)"
            : status;
    }

    private string BuildResultSummary()
    {
        var succeeded = Items.Count(item => item.State == BatchItemState.Completed);
        var failed = Items.Count(item => item.State == BatchItemState.Failed);
        var skipped = Items.Count(item => item.State == BatchItemState.Canceled);

        var parts = new List<string> { $"{succeeded} of {Items.Count} uninstalled successfully" };
        if (failed > 0)
        {
            parts.Add($"{failed} failed");
        }

        if (skipped > 0)
        {
            parts.Add($"{skipped} skipped");
        }

        parts.Add(Leftovers.Count == 0
            ? "no leftover items were found"
            : $"{Leftovers.Count} leftover item(s) across {_owners.Values.Distinct().Count()} application(s) are ready for review");

        return string.Join(" · ", parts) + ".";
    }

    private void AttachSelectionTracking()
    {
        foreach (var item in Leftovers)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(LeftoverItemViewModel.IsSelected) or nameof(LeftoverItemViewModel.SizeBytes))
                {
                    UpdateSelectedTotals();
                }
            };
        }
    }

    private void SetSelection(Func<LeftoverItemViewModel, bool> predicate)
    {
        foreach (var item in Leftovers)
        {
            item.IsSelected = predicate(item);
        }

        UpdateSelectedTotals();
    }

    private void UpdateSelectedTotals()
    {
        SelectedCount = Leftovers.Count(item => item.IsSelected);
        SelectedBytes = Leftovers.Where(item => item.IsSelected).Sum(item => item.SizeBytes ?? 0);
    }

    private void StartSizeComputation()
    {
        var pending = Leftovers.Take(SizeComputationLimit).ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        SizeComputationRunning = true;
        var dispatcher = Application.Current?.Dispatcher;

        _ = Task.Run(() =>
        {
            foreach (var item in pending)
            {
                var size = SizeCalculator.GetSize(item.Path);
                if (dispatcher is null)
                {
                    continue;
                }

                dispatcher.Invoke(() => item.SizeBytes = size > 0 ? size : null);
            }

            dispatcher?.Invoke(() =>
            {
                SizeComputationRunning = false;
                UpdateSelectedTotals();
            });
        });
    }

    private void AddLog(string message)
    {
        Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (Log.Count > 500)
        {
            Log.RemoveAt(0);
        }
    }
}
