namespace VibeUninstaller.Core.Models;

/// <summary>Which slice of the installed applications a list shows.</summary>
public enum AppListView
{
    /// <summary>Classic desktop applications (registry + Steam), excluding updates.</summary>
    Programs,

    /// <summary>Microsoft Store / MSIX packages, excluding system components.</summary>
    Store,

    /// <summary>Windows update style entries ("Update for ...").</summary>
    Updates,

    /// <summary>Everything, including system components and updates.</summary>
    Everything,
}

/// <summary>
/// Filtering rules shared by the application list. Kept in Core so the GUI,
/// the CLI and the tests all agree on what each view means.
/// </summary>
public static class AppListFilters
{
    /// <summary>Applications at or above this size count as large. One gibibyte.</summary>
    public const long LargeAppThresholdBytes = 1_073_741_824;

    /// <summary>How far back "recently installed" reaches.</summary>
    public const int RecentInstallDays = 90;

    /// <summary>True when the application belongs to the requested view.</summary>
    public static bool MatchesView(InstalledApp app, AppListView view) => view switch
    {
        AppListView.Store => app.Source == AppSource.Appx && !app.IsSystemComponent,
        AppListView.Updates => app.IsUpdate,
        AppListView.Everything => true,
        _ => app.Source != AppSource.Appx && !app.IsUpdate,
    };

    /// <summary>Case-insensitive name/publisher search. Blank search matches everything.</summary>
    public static bool MatchesSearch(InstalledApp app, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var needle = search.Trim();
        return app.DisplayName.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
            || (app.Publisher?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    /// <summary>Applies the optional size and age quick filters.</summary>
    public static bool MatchesQuickFilters(
        InstalledApp app,
        bool largeAppsOnly,
        bool recentlyInstalledOnly,
        DateOnly today)
    {
        if (largeAppsOnly && (app.EstimatedSizeBytes ?? 0) < LargeAppThresholdBytes)
        {
            return false;
        }

        if (recentlyInstalledOnly)
        {
            if (app.InstallDate is not { } installed)
            {
                return false;
            }

            if (installed < today.AddDays(-RecentInstallDays) || installed > today)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Full filter pipeline: view, search text and quick filters.</summary>
    public static IEnumerable<InstalledApp> Apply(
        IEnumerable<InstalledApp> apps,
        AppListView view,
        string? search,
        bool largeAppsOnly = false,
        bool recentlyInstalledOnly = false,
        DateOnly? today = null)
    {
        var reference = today ?? DateOnly.FromDateTime(DateTime.Now);
        return apps.Where(app =>
            MatchesView(app, view)
            && MatchesSearch(app, search)
            && MatchesQuickFilters(app, largeAppsOnly, recentlyInstalledOnly, reference));
    }
}
