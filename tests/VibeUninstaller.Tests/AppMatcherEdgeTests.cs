using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Tests;

/// <summary>Filter flags, suggestion quality and ambiguity ordering in AppMatcher.</summary>
public class AppMatcherEdgeTests
{
    private static InstalledApp App(
        string name,
        string? version = null,
        AppSource source = AppSource.Registry,
        bool isUpdate = false,
        bool isSystemComponent = false) => new()
    {
        Id = $"{source}|{name}|{version}",
        DisplayName = name,
        Version = version,
        Source = source,
        IsUpdate = isUpdate,
        IsSystemComponent = isSystemComponent,
    };

    [Fact]
    public void A_null_query_is_reported_as_empty()
    {
        var result = AppMatcher.Match(new[] { App("7-Zip") }, null!);

        Assert.Equal(AppMatchKind.None, result.Kind);
        Assert.NotNull(result.Message);
        Assert.Contains("must not be empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_punctuation_only_query_suggests_nothing()
    {
        var result = AppMatcher.Match(new[] { App("7-Zip") }, "!!!");

        Assert.Equal(AppMatchKind.None, result.Kind);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Near_suggestions_need_a_first_word_of_three_characters()
    {
        var apps = new[] { App("7-Zip"), App("Contoso Suite") };

        // "7" is too short to suggest anything.
        Assert.Empty(AppMatcher.Match(apps, "7 somethingelse").Candidates);

        // "Contoso" is long enough, so the app is offered as a near suggestion.
        var near = AppMatcher.Match(apps, "Contoso somethingelse");
        Assert.Equal(AppMatchKind.None, near.Kind);
        var candidate = Assert.Single(near.Candidates);
        Assert.Equal("Contoso Suite", candidate.DisplayName);
    }

    [Fact]
    public void Filter_respects_each_include_flag()
    {
        var apps = new[]
        {
            App("Store App", source: AppSource.Appx),
            App("Steam Game", source: AppSource.Steam),
            App("Update for X", isUpdate: true),
            App("System Component", isSystemComponent: true),
            App("Plain"),
        };

        var defaults = AppMatcher.Filter(apps);
        Assert.Equal(
            new[] { "Store App", "Steam Game", "Plain" },
            defaults.Select(app => app.DisplayName).ToArray());

        var withoutSteam = AppMatcher.Filter(apps, AppMatcher.Default with { IncludeSteam = false });
        Assert.Equal(
            new[] { "Store App", "Plain" },
            withoutSteam.Select(app => app.DisplayName).ToArray());

        var everything = AppMatcher.Filter(
            apps,
            AppMatcher.Default with { IncludeSystem = true, IncludeStore = true, IncludeSteam = true });
        Assert.Equal(5, everything.Count);
    }

    [Fact]
    public void Equal_names_are_ambiguous_and_sorted_by_version()
    {
        var apps = new[]
        {
            App("Contoso Suite", "2.0"),
            App("Contoso Suite", "1.0"),
        };

        var result = AppMatcher.Match(apps, "contoso suite");

        Assert.Equal(AppMatchKind.Ambiguous, result.Kind);
        Assert.Equal(new[] { "1.0", "2.0" }, result.Candidates.Select(app => app.Version).ToArray());
    }

    [Fact]
    public void Id_matches_win_over_name_matches()
    {
        var target = App("Contoso Suite", "1.0");
        var other = App("Contoso Suite", "2.0");

        var result = AppMatcher.Match(new[] { other, target }, target.Id);

        Assert.Equal(AppMatchKind.Single, result.Kind);
        Assert.Equal("1.0", result.App!.Version);
    }
}
