using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;
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
    private AppListView _view = AppListView.Programs;

    [ObservableProperty]
    private bool _largeAppsOnly;

    [ObservableProperty]
    private bool _recentlyInstalledOnly;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _warningText = string.Empty;

    [ObservableProperty]
    private string _filterSummaryText = string.Empty;

    [ObservableProperty]
    private AppItemViewModel? _selectedApp;

    [ObservableProperty]
    private int _programsCount;

    [ObservableProperty]
    private int _storeCount;

    [ObservableProperty]
    private int _updatesCount;

    [ObservableProperty]
    private int _everythingCount;

    public bool IsElevated { get; } = Windows.Execution.AdminCheck.IsElevated;

    [ObservableProperty]
    private string _elevationText = Windows.Execution.AdminCheck.IsElevated
        ? "Administrator"
        : "Standard user — restart as administrator for full cleanup";

    public string VersionText { get; } = FormatVersion();

    /// <summary>True when the search box should show its placeholder.</summary>
    public bool IsSearchEmpty => string.IsNullOrWhiteSpace(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchEmpty));
        ApplyFilter();
    }

    partial void OnViewChanged(AppListView value) => ApplyFilter();

    partial void OnLargeAppsOnlyChanged(bool value) => ApplyFilter();

    partial void OnRecentlyInstalledOnlyChanged(bool value) => ApplyFilter();

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
            UpdateViewCounts();
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
    private void SetView(AppListView view) => View = view;

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

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

    private void UpdateViewCounts()
    {
        ProgramsCount = _allApps.Count(item => AppListFilters.MatchesView(item.App, AppListView.Programs));
        StoreCount = _allApps.Count(item => AppListFilters.MatchesView(item.App, AppListView.Store));
        UpdatesCount = _allApps.Count(item => AppListFilters.MatchesView(item.App, AppListView.Updates));
        EverythingCount = _allApps.Count(item => AppListFilters.MatchesView(item.App, AppListView.Everything));
    }

    private void ApplyFilter()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var query = _allApps
            .Where(item =>
                AppListFilters.MatchesView(item.App, View)
                && AppListFilters.MatchesSearch(item.App, SearchText)
                && AppListFilters.MatchesQuickFilters(item.App, LargeAppsOnly, RecentlyInstalledOnly, today))
            .ToList();

        Apps.Clear();
        foreach (var app in query)
        {
            Apps.Add(app);
        }

        StatusText = $"{query.Count:N0} of {_allApps.Count:N0} applications";
        FilterSummaryText = query.Count == 0
            ? "nothing matches the current filters"
            : $"{SizeFormat.Bytes(query.Sum(app => app.EstimatedSizeBytes ?? 0))} estimated on disk";
        SelectedApp = null;
    }

    private static string FormatVersion()
    {
        var version = typeof(MainViewModel).Assembly.GetName().Version;
        return version is null ? "dev" : $"v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
