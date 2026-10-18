using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>
/// The list filters behind the GUI sidebar: which view shows what, search
/// matching and the size/age quick filters.
/// </summary>
public class AppListFiltersTests
{
    private static InstalledApp App(
        string name,
        AppSource source = AppSource.Registry,
        bool isUpdate = false,
        bool isSystemComponent = false,
        long? sizeBytes = null,
        DateOnly? installDate = null,
        string? publisher = null) => new()
    {
        Id = $"{source}|{name}",
        DisplayName = name,
        Publisher = publisher,
        Source = source,
        IsUpdate = isUpdate,
        IsSystemComponent = isSystemComponent,
        EstimatedSizeBytes = sizeBytes,
        InstallDate = installDate,
    };

    [Fact]
    public void Programs_view_excludes_store_packages_and_updates()
    {
        Assert.False(AppListFilters.MatchesView(App("WhatsApp", AppSource.Appx), AppListView.Programs));
        Assert.False(AppListFilters.MatchesView(App("Update for Office", isUpdate: true), AppListView.Programs));
        Assert.True(AppListFilters.MatchesView(App("7-Zip"), AppListView.Programs));
        Assert.True(AppListFilters.MatchesView(App("Team Fortress 2", AppSource.Steam), AppListView.Programs));
    }

    [Fact]
    public void Store_view_keeps_only_non_system_store_packages()
    {
        Assert.True(AppListFilters.MatchesView(App("WhatsApp", AppSource.Appx), AppListView.Store));
        Assert.False(AppListFilters.MatchesView(App("Windows Web Experience", AppSource.Appx, isSystemComponent: true), AppListView.Store));
        Assert.False(AppListFilters.MatchesView(App("7-Zip"), AppListView.Store));
    }

    [Fact]
    public void Updates_view_keeps_only_update_entries()
    {
        Assert.True(AppListFilters.MatchesView(App("Update for Office", isUpdate: true), AppListView.Updates));
        Assert.False(AppListFilters.MatchesView(App("7-Zip"), AppListView.Updates));
    }

    [Fact]
    public void Everything_view_keeps_everything()
    {
        Assert.True(AppListFilters.MatchesView(App("7-Zip"), AppListView.Everything));
        Assert.True(AppListFilters.MatchesView(App("WhatsApp", AppSource.Appx, isSystemComponent: true), AppListView.Everything));
        Assert.True(AppListFilters.MatchesView(App("Update for Office", isUpdate: true), AppListView.Everything));
    }

    [Fact]
    public void Search_matches_name_and_publisher_case_insensitively()
    {
        var app = App("VLC media player", publisher: "VideoLAN");

        Assert.True(AppListFilters.MatchesSearch(app, "vlc"));
        Assert.True(AppListFilters.MatchesSearch(app, "VIDEOLAN"));
        Assert.True(AppListFilters.MatchesSearch(app, "Media"));
        Assert.True(AppListFilters.MatchesSearch(app, "vlc media"));
        Assert.False(AppListFilters.MatchesSearch(app, "firefox"));
    }

    [Fact]
    public void Search_is_trimmed_and_blank_search_matches_everything()
    {
        var app = App("VLC media player");

        Assert.True(AppListFilters.MatchesSearch(app, "  vlc  "));
        Assert.True(AppListFilters.MatchesSearch(app, null));
        Assert.True(AppListFilters.MatchesSearch(app, string.Empty));
        Assert.True(AppListFilters.MatchesSearch(app, "   "));
    }

    [Fact]
    public void Search_never_crashes_on_apps_without_a_publisher()
    {
        var app = App("Anonymous tool");

        Assert.False(AppListFilters.MatchesSearch(app, "contoso"));
        Assert.True(AppListFilters.MatchesSearch(app, "anonymous"));
    }

    [Fact]
    public void The_large_filter_uses_the_exact_threshold()
    {
        var today = new DateOnly(2026, 1, 15);

        Assert.Equal(1_073_741_824, AppListFilters.LargeAppThresholdBytes);
        Assert.True(AppListFilters.MatchesQuickFilters(
            App("Big", sizeBytes: AppListFilters.LargeAppThresholdBytes), largeAppsOnly: true, recentlyInstalledOnly: false, today));
        Assert.False(AppListFilters.MatchesQuickFilters(
            App("Almost big", sizeBytes: AppListFilters.LargeAppThresholdBytes - 1), largeAppsOnly: true, recentlyInstalledOnly: false, today));
        Assert.False(AppListFilters.MatchesQuickFilters(
            App("Unknown size"), largeAppsOnly: true, recentlyInstalledOnly: false, today));
    }

    [Fact]
    public void The_recent_filter_starts_at_exactly_ninety_days()
    {
        var today = new DateOnly(2026, 1, 15);

        Assert.True(AppListFilters.MatchesQuickFilters(
            App("Fresh", installDate: today.AddDays(-AppListFilters.RecentInstallDays)),
            largeAppsOnly: false,
            recentlyInstalledOnly: true,
            today));
        Assert.False(AppListFilters.MatchesQuickFilters(
            App("Old", installDate: today.AddDays(-AppListFilters.RecentInstallDays - 1)),
            largeAppsOnly: false,
            recentlyInstalledOnly: true,
            today));
        Assert.False(AppListFilters.MatchesQuickFilters(
            App("No date"), largeAppsOnly: false, recentlyInstalledOnly: true, today));
        Assert.False(AppListFilters.MatchesQuickFilters(
            App("From the future", installDate: today.AddDays(1)),
            largeAppsOnly: false,
            recentlyInstalledOnly: true,
            today));
    }

    [Fact]
    public void Disabled_quick_filters_never_reject_an_app()
    {
        var app = App("No size, no date");

        Assert.True(AppListFilters.MatchesQuickFilters(app, largeAppsOnly: false, recentlyInstalledOnly: false, today: new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void Apply_combines_view_search_and_quick_filters()
    {
        var today = new DateOnly(2026, 1, 15);
        var apps = new[]
        {
            App("Contoso Big", sizeBytes: 2L * 1024 * 1024 * 1024),
            App("Contoso Small", sizeBytes: 10),
            App("Contoso Store", AppSource.Appx, sizeBytes: 2L * 1024 * 1024 * 1024),
            App("Other Big", publisher: "Contoso Ltd", sizeBytes: 2L * 1024 * 1024 * 1024),
        };

        var result = AppListFilters
            .Apply(apps, AppListView.Programs, "contoso", largeAppsOnly: true, recentlyInstalledOnly: false, today)
            .Select(app => app.DisplayName)
            .ToArray();

        Assert.Equal(new[] { "Contoso Big", "Other Big" }, result);
    }

    [Fact]
    public void Apply_without_search_keeps_everything_the_view_allows_and_the_order()
    {
        var apps = new[]
        {
            App("Zulu"),
            App("Alpha"),
            App("Update for Zulu", isUpdate: true),
        };

        var result = AppListFilters.Apply(apps, AppListView.Programs, search: null);

        Assert.Equal(new[] { "Zulu", "Alpha" }, result.Select(app => app.DisplayName).ToArray());
    }
}
