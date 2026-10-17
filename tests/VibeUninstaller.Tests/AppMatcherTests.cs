using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

public class AppMatcherTests
{
    private static readonly InstalledApp[] Apps =
    {
        new() { Id = "reg|HKLM|7-Zip", DisplayName = "7-Zip 24.09 (x64)", Version = "24.09" },
        new() { Id = "reg|HKLM|VLC", DisplayName = "VLC media player", Version = "3.0.21" },
        new() { Id = "reg|HKCU|VLC-nightly", DisplayName = "VLC nightly", Version = "4.0" },
        new() { Id = "reg|HKLM|VSCode", DisplayName = "Microsoft Visual Studio Code" },
        new() { Id = "appx|Contoso", DisplayName = "Contoso Notes", Source = AppSource.Appx, Scope = InstallScope.User },
        new() { Id = "reg|HKLM|Update", DisplayName = "Update for Windows", IsUpdate = true },
    };

    [Fact]
    public void An_exact_id_wins()
    {
        var result = AppMatcher.Match(Apps, "reg|HKLM|VLC");

        Assert.Equal(AppMatchKind.Single, result.Kind);
        Assert.Equal("VLC media player", result.App!.DisplayName);
    }

    [Fact]
    public void An_exact_name_wins_over_a_partial_one()
    {
        var result = AppMatcher.Match(Apps, "VLC nightly");

        Assert.Equal(AppMatchKind.Single, result.Kind);
        Assert.Equal("VLC nightly", result.App!.DisplayName);
    }

    [Fact]
    public void A_unique_substring_is_enough()
    {
        var result = AppMatcher.Match(Apps, "7-Zip");

        Assert.Equal(AppMatchKind.Single, result.Kind);
        Assert.Equal("7-Zip 24.09 (x64)", result.App!.DisplayName);
    }

    [Fact]
    public void Several_matches_are_reported_as_ambiguous()
    {
        var result = AppMatcher.Match(Apps, "VLC");

        Assert.Equal(AppMatchKind.Ambiguous, result.Kind);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Null(result.App);
    }

    [Fact]
    public void Word_order_and_punctuation_do_not_matter()
    {
        var result = AppMatcher.Match(Apps, "code studio visual");

        Assert.Equal(AppMatchKind.Single, result.Kind);
        Assert.Equal("Microsoft Visual Studio Code", result.App!.DisplayName);
    }

    [Fact]
    public void Updates_and_system_components_are_hidden_by_default()
    {
        Assert.Equal(AppMatchKind.None, AppMatcher.Match(Apps, "Update for Windows").Kind);
        Assert.Equal(AppMatchKind.Single, AppMatcher.Match(Apps, "Update for Windows", new AppMatchOptions { IncludeSystem = true }).Kind);
    }

    [Fact]
    public void Store_packages_can_be_excluded()
    {
        var options = new AppMatchOptions { IncludeStore = false };

        Assert.Equal(AppMatchKind.None, AppMatcher.Match(Apps, "Contoso", options).Kind);
        Assert.Equal(AppMatchKind.Single, AppMatcher.Match(Apps, "Contoso").Kind);
    }

    [Fact]
    public void An_unknown_name_returns_suggestions()
    {
        var result = AppMatcher.Match(Apps, "vlc player deluxe");

        Assert.Equal(AppMatchKind.None, result.Kind);
        Assert.NotEmpty(result.Candidates);
        Assert.Null(result.App);
    }

    [Fact]
    public void An_empty_query_is_rejected()
    {
        Assert.Equal(AppMatchKind.None, AppMatcher.Match(Apps, "   ").Kind);
    }
}
