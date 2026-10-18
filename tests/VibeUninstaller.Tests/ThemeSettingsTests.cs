using VibeUninstaller.App.Services;

namespace VibeUninstaller.Tests;

public class ThemeSettingsTests
{
    [Fact]
    public void Catalog_has_several_distinct_themes()
    {
        Assert.True(ThemeCatalog.All.Count >= 3, "the user asked for multiple themes");
        Assert.Equal(
            ThemeCatalog.All.Count,
            ThemeCatalog.All.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            ThemeCatalog.All.Count,
            ThemeCatalog.All.Select(theme => theme.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ThemeCatalog.All, theme => Assert.EndsWith(".xaml", theme.FileName));
        Assert.All(ThemeCatalog.All, theme => Assert.False(string.IsNullOrWhiteSpace(theme.DisplayName)));
    }

    [Theory]
    [InlineData("dark", "dark")]
    [InlineData("LIGHT", "light")]
    [InlineData(" midnight ", "midnight")]
    [InlineData("Ocean", "ocean")]
    public void Resolve_is_case_insensitive(string id, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.Resolve(id).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("solarized")]
    public void Resolve_falls_back_to_the_default_theme(string? id)
    {
        Assert.Equal(ThemeCatalog.DefaultId, ThemeCatalog.Resolve(id).Id);
    }

    [Fact]
    public void Theme_sources_are_pack_uris_inside_the_app_assembly()
    {
        var source = ThemeManager.BuildSource(ThemeCatalog.All[1]);

        Assert.Equal("pack", source.Scheme);
        Assert.Contains("/Resources/Themes/", source.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(ThemeCatalog.All[1].FileName, source.OriginalString);
    }

    [Fact]
    public void Settings_round_trip_through_json()
    {
        var json = AppSettingsJson.Compose(new AppSettings("ocean"));

        Assert.Contains("\"theme\"", json);
        Assert.Equal("ocean", AppSettingsJson.Parse(json).ThemeId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("{\"theme\":42}")]
    [InlineData("{\"other\":\"light\"}")]
    [InlineData("{\"theme\":\"solarized\"}")]
    public void Broken_or_unknown_settings_fall_back_to_the_default(string? json)
    {
        Assert.Equal(ThemeCatalog.DefaultId, AppSettingsJson.Parse(json).ThemeId);
    }

    [Fact]
    public void Settings_keep_a_known_theme_even_with_different_casing()
    {
        Assert.Equal("midnight", AppSettingsJson.Parse("{\"theme\":\"MIDNIGHT\"}").ThemeId);
    }

    [Fact]
    public void Settings_round_trip_the_registry_backup_choice()
    {
        var json = AppSettingsJson.Compose(new AppSettings("dark") { BackupRegistryKeys = false });

        Assert.Contains("backupRegistryKeys", json);
        Assert.False(AppSettingsJson.Parse(json).BackupRegistryKeys);
    }

    [Fact]
    public void Registry_backups_default_to_on()
    {
        Assert.True(AppSettings.Default.BackupRegistryKeys);
        Assert.True(AppSettingsJson.Parse("{\"theme\":\"dark\"}").BackupRegistryKeys);
        Assert.True(AppSettingsJson.Parse("{\"backupRegistryKeys\":\"nonsense\"}").BackupRegistryKeys);
        Assert.True(AppSettingsJson.Parse("{}").BackupRegistryKeys);
    }

    [Fact]
    public void A_settings_file_without_a_theme_keeps_the_other_choices()
    {
        var settings = AppSettingsJson.Parse("{\"backupRegistryKeys\":false}");

        Assert.Equal(ThemeCatalog.DefaultId, settings.ThemeId);
        Assert.False(settings.BackupRegistryKeys);
    }

    [Fact]
    public void Registry_backups_are_on_by_default_for_removal()
    {
        Assert.True(new VibeUninstaller.Core.Models.LeftoverRemovalOptions().BackupRegistryKeys);
    }
}
