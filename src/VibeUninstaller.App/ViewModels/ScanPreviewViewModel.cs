using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;
using VibeUninstaller.Windows.Execution;

namespace VibeUninstaller.App.ViewModels;

/// <summary>
/// Fingerprint-only scan preview: shows what could be cleaned up before an
/// uninstaller is ever run, and never changes anything itself.
/// </summary>
public sealed partial class ScanPreviewViewModel : ObservableObject
{
    private readonly StaticScanner _scanner = new();

    public ScanPreviewViewModel(InstalledApp app)
    {
        App = app;
        Item = new AppItemViewModel(app);
    }

    public InstalledApp App { get; }

    /// <summary>Reuses the list row view model for the header lettermark and labels.</summary>
    public AppItemViewModel Item { get; }

    public ObservableCollection<LeftoverItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Preparing scan…";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public bool IsNotScanning => !IsScanning;

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(IsNotScanning));

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        IsScanning = true;
        StatusText = "Building a fingerprint and walking the places this application can still own…";

        try
        {
            var progress = new Progress<string>(message => StatusText = message);
            var result = await Task.Run(
                () => _scanner.Scan(App, progress, cancellationToken),
                cancellationToken).ConfigureAwait(true);

            Items.Clear();
            foreach (var item in result.Items)
            {
                Items.Add(new LeftoverItemViewModel(item));
            }

            SummaryText = Items.Count == 0
                ? "No leftovers found. This application looks clean on disk and in the registry."
                : $"{Items.Count:N0} item(s) found · {SizeFormat.Bytes(result.TotalSizeBytes)} · "
                    + $"{Items.Count(i => i.Tier == RiskTier.Safe)} safe, "
                    + $"{Items.Count(i => i.Tier == RiskTier.Moderate)} moderate, "
                    + $"{Items.Count(i => i.Tier == RiskTier.Advanced)} advanced · "
                    + $"scanned in {result.Duration.TotalSeconds:N1}s";

            StatusText = "Scan complete — nothing was changed.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        catch (Exception exception)
        {
            SummaryText = "The scan failed. See the status line for details.";
            StatusText = $"Scan failed: {exception.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }
}
