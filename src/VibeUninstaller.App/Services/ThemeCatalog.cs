using System.Collections.Generic;
using System.Linq;

namespace VibeUninstaller.App.Services;

/// <summary>One selectable color theme.</summary>
public sealed record ThemeOption(string Id, string DisplayName, string FileName);

/// <summary>The built-in themes; ids are stable and persisted in settings.</summary>
public static class ThemeCatalog
{
    public const string DefaultId = "dark";

    public static IReadOnlyList<ThemeOption> All { get; } = new[]
    {
        new ThemeOption("dark", "Dark", "Dark.xaml"),
        new ThemeOption("light", "Light", "Light.xaml"),
        new ThemeOption("midnight", "Midnight (OLED)", "Midnight.xaml"),
        new ThemeOption("ocean", "Ocean", "Ocean.xaml"),
    };

    /// <summary>Falls back to the default theme for unknown or missing ids.</summary>
    public static ThemeOption Resolve(string? id) =>
        All.FirstOrDefault(option => string.Equals(option.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}
