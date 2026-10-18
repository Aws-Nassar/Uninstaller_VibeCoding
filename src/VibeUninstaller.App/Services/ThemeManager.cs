using System.IO.Packaging;
using System.Windows;

namespace VibeUninstaller.App.Services;

/// <summary>
/// Swaps the merged theme dictionary inside a resource dictionary chain and
/// remembers the choice in the settings file.
/// </summary>
public static class ThemeManager
{
    private const string ThemeFolderMarker = "/Resources/Themes/";

    public static ThemeOption Current { get; private set; } = ThemeCatalog.Resolve(null);

    /// <summary>Raised after a theme was applied to the application resources.</summary>
    public static event EventHandler<ThemeOption>? Changed;

    /// <summary>Applies the theme stored in the settings file without rewriting it.</summary>
    public static void ApplyInitial(ResourceDictionary target) =>
        Apply(target, AppSettingsStore.Load().ThemeId, persist: false);

    public static void Apply(ResourceDictionary target, string? id, bool persist = true)
    {
        ArgumentNullException.ThrowIfNull(target);

        var option = ThemeCatalog.Resolve(id);
        var existing = FindThemeDictionary(target);
        if (existing is not null)
        {
            target.MergedDictionaries.Remove(existing);
        }

        target.MergedDictionaries.Add(new ResourceDictionary { Source = BuildSource(option) });

        Current = option;
        if (persist)
        {
            AppSettingsStore.Save(new AppSettings(option.Id));
        }

        Changed?.Invoke(null, option);
    }

    /// <summary>The absolute pack URI of a theme dictionary; used by tests too.</summary>
    public static Uri BuildSource(ThemeOption option)
    {
        // Touching PackUriHelper initializes the WPF parser for the "pack"
        // scheme; without it System.Uri rejects the "application:,,," authority
        // outside of a fully started WPF application.
        _ = PackUriHelper.UriSchemePack;
        return new Uri(
            $"pack://application:,,,/VibeUninstaller;component/Resources/Themes/{option.FileName}",
            UriKind.Absolute);
    }

    private static ResourceDictionary? FindThemeDictionary(ResourceDictionary target)
    {
        foreach (var dictionary in target.MergedDictionaries)
        {
            if (dictionary.Source?.OriginalString.Contains(ThemeFolderMarker, StringComparison.OrdinalIgnoreCase) == true)
            {
                return dictionary;
            }
        }

        return null;
    }
}
