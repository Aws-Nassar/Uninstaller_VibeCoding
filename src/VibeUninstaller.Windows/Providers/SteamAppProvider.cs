using Microsoft.Win32;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Providers;

/// <summary>Discovers Steam games from the local Steam libraries.</summary>
public sealed class SteamAppProvider : IAppProvider
{
    private const int SteamworksCommonRedistributables = 228980;

    public string Name => "Steam";

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<InstalledApp>>(() => Read(cancellationToken), cancellationToken);

    private static List<InstalledApp> Read(CancellationToken cancellationToken)
    {
        var apps = new List<InstalledApp>();
        var steamRoot = FindSteamRoot();
        if (steamRoot is null)
        {
            return apps;
        }

        foreach (var library in FindLibraries(steamRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var app = ParseManifest(manifest, steamApps);
                    if (app is not null)
                    {
                        apps.Add(app);
                    }
                }
                catch (Exception)
                {
                    // A corrupt manifest should never break the whole list.
                }
            }
        }

        return apps;
    }

    private static InstalledApp? ParseManifest(string manifestPath, string steamAppsPath)
    {
        var content = File.ReadAllText(manifestPath);
        var parsed = VdfParser.Parse(content);
        var state = parsed.TryGetValue("AppState", out var appState) && appState is Dictionary<string, object> dict
            ? dict
            : parsed;

        var appId = GetString(state, "appid");
        var name = GetString(state, "name");
        var installDir = GetString(state, "installdir");
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!int.TryParse(appId, out var numericId) || numericId == SteamworksCommonRedistributables)
        {
            return null;
        }

        var sizeBytes = long.TryParse(GetString(state, "SizeOnDisk"), out var size) ? size : 0;
        DateOnly? updated = null;
        if (long.TryParse(GetString(state, "LastUpdated"), out var updatedSeconds) && updatedSeconds > 0)
        {
            updated = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(updatedSeconds).DateTime);
        }

        var installLocation = string.IsNullOrWhiteSpace(installDir)
            ? null
            : Path.Combine(steamAppsPath, "common", installDir);

        return new InstalledApp
        {
            Id = $"steam|{appId}",
            DisplayName = name,
            Publisher = null,
            InstallLocation = installLocation,
            EstimatedSizeBytes = sizeBytes > 0 ? sizeBytes : null,
            InstallDate = updated,
            SteamAppId = appId,
            UninstallString = $"steam://uninstall/{appId}",
            Source = AppSource.Steam,
            Scope = InstallScope.Machine,
        };
    }

    private static string? FindSteamRoot()
    {
        using (var currentUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
        {
            if (currentUser?.GetValue("SteamPath") is string steamPath)
            {
                var normalized = PathTools.Normalize(steamPath);
                if (Directory.Exists(normalized))
                {
                    return normalized;
                }
            }
        }

        using (var machine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
        {
            if (machine?.GetValue("InstallPath") is string installPath)
            {
                var normalized = PathTools.Normalize(installPath);
                if (Directory.Exists(normalized))
                {
                    return normalized;
                }
            }
        }

        var fallback = PathTools.Normalize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static IEnumerable<string> FindLibraries(string steamRoot)
    {
        var libraries = new List<string> { steamRoot };
        var candidates = new[]
        {
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            Dictionary<string, object> parsed;
            try
            {
                parsed = VdfParser.Parse(File.ReadAllText(candidate));
            }
            catch (Exception)
            {
                continue;
            }

            var root = parsed.TryGetValue("libraryfolders", out var folders) && folders is Dictionary<string, object> dict
                ? dict
                : parsed;

            foreach (var entry in root.Values)
            {
                var path = entry switch
                {
                    string direct => direct,
                    Dictionary<string, object> nested => GetString(nested, "path"),
                    _ => null,
                };

                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var normalized = PathTools.Normalize(path);
                if (Directory.Exists(normalized) && !libraries.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    libraries.Add(normalized);
                }
            }

            break;
        }

        return libraries;
    }

    private static string? GetString(Dictionary<string, object> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) ? value as string : null;
}
