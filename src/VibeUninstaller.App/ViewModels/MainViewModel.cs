using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Windows.Providers;

namespace VibeUninstaller.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppEnumerator _enumerator = new();
    private readonly List<AppItemViewModel> _allApps = new();

    public ObservableCollection<AppItemViewModel> Apps { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _viewFilter = "Programs";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _warningText = string.Empty;

    [ObservableProperty]
    private AppItemViewModel? _selectedApp;

    [ObservableProperty]
    private string _elevationText = Windows.Execution.AdminCheck.IsElevated
        ? "Administrator"
        : "Standard user — restart as administrator for full cleanup";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnViewFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Scanning installed applications...";

        try
        {
            var result = await _enumerator.GetInstalledAppsAsync().ConfigureAwait(true);

            _allApps.Clear();
            foreach (var app in result.Apps)
            {
                _allApps.Add(new AppItemViewModel(app));
            }

            WarningText = result.Warnings.Count > 0 ? string.Join(" · ", result.Warnings) : string.Empty;
            ApplyFilter();
        }
        catch (Exception exception)
        {
            StatusText = $"Scan failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetViewFilter(string filter) => ViewFilter = filter;

    [RelayCommand]
    private void OpenInstallFolder()
    {
        var location = SelectedApp?.App.InstallLocation;
        if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{location}\"")
        {
            UseShellExecute = true,
        });
    }

    private void ApplyFilter()
    {
        IEnumerable<AppItemViewModel> query = _allApps;

        query = ViewFilter switch
        {
            "Store" => query.Where(app => app.App.Source == AppSource.Appx && !app.App.IsSystemComponent),
            "Updates" => query.Where(app => app.App.IsUpdate),
            "All" => query,
            _ => query.Where(app => app.App.Source != AppSource.Appx && !app.App.IsUpdate),
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            query = query.Where(app =>
                app.DisplayName.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                || app.Publisher.Contains(needle, StringComparison.CurrentCultureIgnoreCase));
        }

        Apps.Clear();
        foreach (var app in query)
        {
            Apps.Add(app);
        }

        StatusText = $"{Apps.Count:N0} of {_allApps.Count:N0} applications";
        SelectedApp = null;
    }
}
