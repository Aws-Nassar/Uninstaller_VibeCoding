using Microsoft.Win32;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Providers;

/// <summary>
/// Reads classic Win32 applications from the Uninstall registry keys
/// (machine and user, 64-bit and 32-bit locations).
/// </summary>
public sealed class RegistryAppProvider : IAppProvider
{
    public string Name => "Registry";

    private static readonly (RegistryHive Hive, string Path, string CanonicalPrefix, InstallScope Scope)[] Locations =
    {
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", InstallScope.Machine),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", InstallScope.Machine),
        (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", InstallScope.User),
        (RegistryHive.CurrentUser, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKCU\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", InstallScope.User),
    };

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<InstalledApp>>(() => Read(cancellationToken), cancellationToken);

    private static List<InstalledApp> Read(CancellationToken cancellationToken)
    {
        var apps = new List<InstalledApp>(512);

        foreach (var (hive, path, canonicalPrefix, scope) in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var uninstallKey = baseKey.OpenSubKey(path, writable: false);
            if (uninstallKey is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var entry = uninstallKey.OpenSubKey(subKeyName, writable: false);
                if (entry is null)
                {
                    continue;
                }

                var app = ParseEntry(entry, subKeyName, canonicalPrefix, scope, hive);
                if (app is not null)
                {
                    apps.Add(app);
                }
            }
        }

        return apps;
    }

    private static InstalledApp? ParseEntry(
        RegistryKey entry,
        string subKeyName,
        string canonicalPrefix,
        InstallScope scope,
        RegistryHive hive)
    {
        var displayName = GetString(entry, "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        if (GetInt(entry, "SystemComponent") == 1 || GetInt(entry, "NoRemove") == 1)
        {
            return null;
        }

        var uninstallString = GetString(entry, "UninstallString");
        var quietUninstallString = GetString(entry, "QuietUninstallString");
        var parentKeyName = GetString(entry, "ParentKeyName");

        if (string.IsNullOrWhiteSpace(uninstallString)
            && string.IsNullOrWhiteSpace(quietUninstallString)
            && string.IsNullOrWhiteSpace(parentKeyName))
        {
            return null;
        }

        var installLocation = GetString(entry, "InstallLocation");
        var productCode = GetProductCode(subKeyName, uninstallString, entry);
        var estimatedSizeKb = GetLong(entry, "EstimatedSize");
        var releaseType = GetString(entry, "ReleaseType");

        var isUpdate =
            !string.IsNullOrWhiteSpace(parentKeyName)
            || (!string.IsNullOrWhiteSpace(releaseType)
                && !releaseType.Equals("Security Update", StringComparison.OrdinalIgnoreCase))
            || displayName.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase)
            || displayName.StartsWith("Security Update for ", StringComparison.OrdinalIgnoreCase);

        var isSystemComponent = !string.IsNullOrWhiteSpace(parentKeyName);

        var source = productCode is not null && PathTools.LooksLikeGuid(productCode)
            ? AppSource.Registry
            : AppSource.Registry;

        return new InstalledApp
        {
            Id = $"reg|{(hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU")}|{subKeyName}",
            DisplayName = displayName.Trim(),
            Version = GetString(entry, "DisplayVersion"),
            Publisher = GetString(entry, "Publisher"),
            InstallLocation = installLocation,
            UninstallString = uninstallString,
            QuietUninstallString = quietUninstallString,
            DisplayIcon = GetString(entry, "DisplayIcon"),
            EstimatedSizeBytes = estimatedSizeKb > 0 ? estimatedSizeKb * 1024 : null,
            InstallDate = ParseInstallDate(GetString(entry, "InstallDate")),
            ProductCode = productCode,
            RegistryKeyPath = $@"{canonicalPrefix}\{subKeyName}",
            UrlInfoAbout = GetString(entry, "URLInfoAbout") ?? GetString(entry, "URLUpdateInfo"),
            InstallSource = GetString(entry, "InstallSource"),
            Source = source,
            Scope = scope,
            IsUpdate = isUpdate,
            IsSystemComponent = isSystemComponent,
        };
    }

    private static string? GetProductCode(string subKeyName, string? uninstallString, RegistryKey entry)
    {
        if (PathTools.LooksLikeGuid(subKeyName))
        {
            return subKeyName;
        }

        if (GetInt(entry, "WindowsInstaller") == 1 && PathTools.LooksLikeGuid(subKeyName))
        {
            return subKeyName;
        }

        var fromCommand = Core.Execution.CommandLineSplitter.TryGetMsiProductCode(uninstallString)
            ?? Core.Execution.CommandLineSplitter.TryGetMsiProductCode(GetString(entry, "QuietUninstallString"));
        return fromCommand;
    }

    private static string? GetString(RegistryKey key, string name) =>
        key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;

    private static int GetInt(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static long GetLong(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        return value switch
        {
            int i => i,
            long l => l,
            string s when long.TryParse(s, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static DateOnly? ParseInstallDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8)
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyyMMdd", out var date) ? date : null;
    }
}
