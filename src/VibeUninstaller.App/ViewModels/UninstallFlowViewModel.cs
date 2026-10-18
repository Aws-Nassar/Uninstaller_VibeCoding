using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Windows.Execution;

namespace VibeUninstaller.App.ViewModels;

public enum FlowState
{
    Confirm,
    Running,
    Review,
    Removing,
    Done,
}

/// <summary>
/// Drives the whole uninstall flow: confirmation, vendor uninstaller,
/// leftover review and removal.
/// </summary>
public sealed partial class UninstallFlowViewModel : ObservableObject
{
    private const int SizeComputationLimit = 400;

    private readonly UninstallService _service = new();
    private readonly CancellationTokenSource _cancellation = new();
    private UninstallSessionResult? _session;

    public UninstallFlowViewModel(InstalledApp app)
    {
        App = app;
        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        Installer = InstallerInfoDetector.Detect(app, parsed);

        ScanLeftovers = CanScanLeftovers;
        UseSilent = false;
        SelectedAppDetails =
            $"{App.Publisher ?? "Unknown publisher"} · {App.Version ?? "unknown version"} · {App.ScopeLabel}";
    }

    public InstalledApp App { get; }

    public InstallerInfo Installer { get; }

    public string SelectedAppDetails { get; }

    public bool CanScanLeftovers => App.SupportsLeftoverScan && App.Source != AppSource.Steam;

    public string ScanNote => App.Source switch
    {
        AppSource.Steam => "Steam games are removed by the Steam client. Use Steam to uninstall, then rescan with VibeUninstaller.",
        AppSource.Appx => "Store apps are removed as a package; the system is cleaned by Windows.",
        _ => "The system is captured before and after, then remaining files and registry keys are scored.",
    };

    public string SilentNote => Installer.SupportsSilent
        ? $"Silent switches available ({Installer.FamilyLabel})"
        : "No silent switches are known for this installer";

    public bool CanUseSilent => Installer.SupportsSilent;

    public bool SomethingChanged { get; private set; }

    public ObservableCollection<string> Log { get; } = new();

    public ObservableCollection<LeftoverItemViewModel> Leftovers { get; } = new();

    [ObservableProperty]
    private FlowState _state = FlowState.Confirm;

    [ObservableProperty]
    private bool _scanLeftovers = true;

    [ObservableProperty]
    private bool _useSilent;

    [ObservableProperty]
    private string _stageText = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _removalSummary = string.Empty;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private long _selectedBytes;

    [ObservableProperty]
    private bool _hasLeftovers;

    [ObservableProperty]
    private bool _sizeComputationRunning;

    public string StateName => State.ToString();

    partial void OnStateChanged(FlowState value) => OnPropertyChanged(nameof(StateName));

    public string SizeComputationText => SizeComputationRunning ? "Computing sizes…" : string.Empty;

    partial void OnSizeComputationRunningChanged(bool value) =>
        OnPropertyChanged(nameof(SizeComputationText));

    public async Task StartAsync()
    {
        State = FlowState.Running;
        StageText = "Starting...";
        Log.Clear();

        var progress = new Progress<UninstallProgress>(update =>
        {
            StageText = UninstallText.DescribeStage(update.Stage);
            if (!string.IsNullOrWhiteSpace(update.Detail))
            {
                AddLog(update.Detail);
            }
        });

        try
        {
            var session = await _service
                .UninstallAsync(App, new UninstallOptions
                {
                    ScanLeftovers = ScanLeftovers,
                    UseSilentUninstall = UseSilent,
                }, progress, _cancellation.Token)
                .ConfigureAwait(true);

            _session = session;
            SomethingChanged = session.Run.Status is UninstallRunStatus.Succeeded or UninstallRunStatus.Failed;
            ResultSummary = BuildRunSummary(session.Run, session.Scan);

            if (session.Scan is { Items.Count: > 0 } scan)
            {
                foreach (var item in scan.Items)
                {
                    Leftovers.Add(new LeftoverItemViewModel(item));
                }

                HasLeftovers = true;
                AttachSelectionTracking();
                State = FlowState.Review;
                UpdateSelectedTotals();
                StartSizeComputation();
            }
            else
            {
                State = FlowState.Done;
            }
        }
        catch (OperationCanceledException)
        {
            ResultSummary = "The uninstall was canceled.";
            State = FlowState.Done;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            ResultSummary = "The uninstall failed.";
            State = FlowState.Done;
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation.Cancel();

    [RelayCommand]
    private void SelectAll() => SetSelection(_ => true);

    [RelayCommand]
    private void SelectNone() => SetSelection(_ => false);

    [RelayCommand]
    private void SelectRecommended() => SetSelection(item => item.Tier != RiskTier.Advanced);

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var selected = Leftovers
            .Where(item => item.IsSelected)
            .Select(item => item.Item with { SizeBytes = item.SizeBytes })
            .ToArray();

        if (selected.Length == 0)
        {
            ErrorText = "Select at least one item to remove.";
            return;
        }

        State = FlowState.Removing;
        StageText = "Removing leftovers...";
        ErrorText = null;

        var progress = new Progress<string>(AddLog);
        try
        {
            var report = await new RemovalExecutor()
                .RemoveAsync(App, selected, progress, CancellationToken.None)
                .ConfigureAwait(true);

            RemovalSummary =
                $"Recycled {report.FilesRemoved:N0} file item(s) ({report.BytesRemoved / 1024.0 / 1024.0:N1} MB) · " +
                $"removed {report.RegistryRemoved:N0} registry key(s) · " +
                $"registry backups in {report.VaultPath}";

            if (report.Errors.Count > 0)
            {
                ErrorText = string.Join(Environment.NewLine, report.Errors.Take(10));
            }

            SomethingChanged = true;
            State = FlowState.Done;
        }
        catch (Exception exception)
        {
            ErrorText = exception.Message;
            State = FlowState.Done;
        }
    }

    [RelayCommand]
    private void OpenVault()
    {
        var vault = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "VibeUninstaller",
            "Vault");

        if (!Directory.Exists(vault))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{vault}\"") { UseShellExecute = true });
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

    private static string BuildRunSummary(UninstallRunResult run, LeftoverScanResult? scan)
    {
        var parts = new List<string>();

        parts.Add(run.Status switch
        {
            UninstallRunStatus.Succeeded => "The uninstaller finished successfully.",
            UninstallRunStatus.Failed => "The uninstaller reported a failure.",
            UninstallRunStatus.TimedOut => "The uninstaller timed out.",
            UninstallRunStatus.Canceled => "The uninstall was canceled.",
            _ => "The uninstaller could not be started.",
        });

        if (run.ExitCode is { } code)
        {
            parts.Add($"Exit code {code}.");
        }

        if (run.RebootRequired)
        {
            parts.Add("A reboot is required to finish the removal.");
        }

        if (!string.IsNullOrWhiteSpace(run.ErrorMessage))
        {
            parts.Add(run.ErrorMessage);
        }

        if (scan is not null)
        {
            parts.Add($"The uninstaller removed {scan.RegistryVanishedCount:N0} registry keys and " +
                      $"{scan.FilesVanishedCount:N0} file system entries. " +
                      $"{scan.SafeCount} certain, {scan.ModerateCount} likely and {scan.AdvancedCount} advanced leftover item(s) were found.");
        }

        return string.Join(" ", parts);
    }
}
