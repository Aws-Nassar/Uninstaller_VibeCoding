using System.IO;
using System.Text.Json;

namespace VibeUninstaller.App.Services;

/// <summary>User choices that survive restarts.</summary>
public sealed record AppSettings(string ThemeId)
{
    public static AppSettings Default { get; } = new(ThemeCatalog.DefaultId);
}

/// <summary>Pure JSON handling for settings; the tests pin these rules.</summary>
public static class AppSettingsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AppSettings.Default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("theme", out var theme)
                || theme.ValueKind != JsonValueKind.String)
            {
                return AppSettings.Default;
            }

            // Resolve so an unknown id in the file cannot break startup.
            return new AppSettings(ThemeCatalog.Resolve(theme.GetString()).Id);
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public static string Compose(AppSettings settings) =>
        JsonSerializer.Serialize(new { theme = settings.ThemeId }, Options);
}

/// <summary>
/// Reads and writes settings under %AppData%\VibeUninstaller. Every failure is
/// swallowed: a locked or read-only profile must not stop the application.
/// </summary>
public static class AppSettingsStore
{
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VibeUninstaller",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            return File.Exists(path)
                ? AppSettingsJson.Parse(File.ReadAllText(path))
                : AppSettings.Default;
        }
        catch (Exception)
        {
            return AppSettings.Default;
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var path = SettingsPath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, AppSettingsJson.Compose(settings));
        }
        catch (Exception)
        {
            // The theme just will not stick; the UI keeps working.
        }
    }
}
