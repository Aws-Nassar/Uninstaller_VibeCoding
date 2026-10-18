namespace VibeUninstaller.Core.Analysis;

/// <summary>
/// Hard safety rules: locations that may never be proposed or deleted, plus a
/// small allow list that overrides the deny list for specific subtrees
/// (for example the Start Menu, which lives under a denied system folder).
/// The deny list is code, not configuration, on purpose.
/// </summary>
public sealed class LeftoverRules
{
    public required IReadOnlyList<string> RegistryRoots { get; init; }

    public required IReadOnlyList<string> RegistryDenyPrefixes { get; init; }

    public required IReadOnlyList<string> RegistryAllowPrefixes { get; init; }

    public required IReadOnlyList<string> FileDenyPrefixes { get; init; }

    public required IReadOnlyList<string> FileAllowPrefixes { get; init; }

    public required IReadOnlySet<string> FileNameDenySet { get; init; }

    public bool IsRegistryDenied(string path)
    {
        // Normalize here as well: the deny list must never depend on the caller
        // remembering to canonicalize (see PathTools.Normalize).
        path = PathTools.Normalize(path);
        if (path.Length == 0)
        {
            return true;
        }

        // The snapshot roots themselves are never candidates.
        foreach (var root in RegistryRoots)
        {
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (MatchesAny(path, RegistryAllowPrefixes))
        {
            return false;
        }

        return MatchesAny(path, RegistryDenyPrefixes);
    }

    public bool IsFileDenied(string path)
    {
        // Normalize here as well: "\\?\C:\Windows\..." and "C:\Windows.\..." are
        // classic ways to slip past prefix checks, so the deny list canonicalizes
        // instead of trusting the caller.
        path = PathTools.Normalize(path);
        if (path.Length == 0)
        {
            return true;
        }

        var name = PathTools.LastSegment(path);
        if (FileNameDenySet.Contains(name))
        {
            return true;
        }

        if (MatchesAny(path, FileAllowPrefixes))
        {
            return false;
        }

        return MatchesAny(path, FileDenyPrefixes);
    }

    /// <summary>
    /// True when a capture may still descend into this path. Denied subtrees are
    /// pruned, except when they contain an allow-listed descendant (for example
    /// the Start Menu lives under a denied system folder).
    /// </summary>
    public bool CanDescendIntoRegistry(string path) =>
        !IsRegistryDenied(path) || IsOnPathToAllowPrefix(path, RegistryAllowPrefixes);

    public bool CanDescendIntoFile(string path) =>
        !IsFileDenied(path) || IsOnPathToAllowPrefix(path, FileAllowPrefixes);

    private static bool IsOnPathToAllowPrefix(string path, IReadOnlyList<string> allowPrefixes)
    {
        foreach (var allow in allowPrefixes)
        {
            if (PathTools.IsUnder(path, allow, includeEqual: true)
                || PathTools.IsUnder(allow, path, includeEqual: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAny(string path, IReadOnlyList<string> prefixes)
    {
        for (var i = 0; i < prefixes.Count; i++)
        {
            if (PathTools.IsUnder(path, prefixes[i], includeEqual: true))
            {
                return true;
            }
        }

        return false;
    }

    public static LeftoverRules CreateDefault(FingerprintEnvironment environment)
    {
        var windows = environment.WindowsDirectory;
        var programFiles = environment.ProgramFiles;
        var programFilesX86 = environment.ProgramFilesX86;
        var programData = environment.ProgramData;
        var localAppData = environment.LocalAppData;
        var roaming = environment.RoamingAppData;
        var drive = environment.SystemDrive.TrimEnd('\\');

        return new LeftoverRules
        {
            RegistryRoots = new[]
            {
                @"HKLM\SOFTWARE",
                @"HKCU\SOFTWARE",
            },
            RegistryDenyPrefixes = new[]
            {
                @"HKLM\SOFTWARE\Microsoft",
                @"HKLM\SOFTWARE\WOW6432Node\Microsoft",
                @"HKCU\SOFTWARE\Microsoft",
                @"HKCU\SOFTWARE\WOW6432Node\Microsoft",
                @"HKLM\SOFTWARE\Classes\Local Settings",
                @"HKCU\SOFTWARE\Classes\Local Settings",
                @"HKLM\SOFTWARE\Classes\Installer",
                @"HKLM\SOFTWARE\Policies",
                @"HKCU\SOFTWARE\Policies",
                @"HKLM\SOFTWARE\RegisteredApplications",
                @"HKCU\SOFTWARE\RegisteredApplications",
            },
            RegistryAllowPrefixes = new[]
            {
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"HKCU\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            },
            FileDenyPrefixes = Prune(new[]
            {
                windows,
                $@"{drive}\$Recycle.Bin",
                $@"{drive}\$WinREAgent",
                $@"{drive}\Recovery",
                $@"{drive}\System Volume Information",
                $@"{programFiles}\WindowsApps",
                $@"{programFiles}\ModifiableWindowsApps",
                $@"{programFiles}\Windows Defender",
                $@"{programFiles}\Windows Mail",
                $@"{programFiles}\Windows NT",
                $@"{programFiles}\Uninstall Information",
                $@"{programFilesX86}\Windows Defender",
                $@"{programData}\Microsoft",
                $@"{programData}\Packages",
                $@"{programData}\Package Cache",
                $@"{programData}\Microsoft OneDrive",
                $@"{localAppData}\Microsoft",
                $@"{localAppData}\Packages",
                $@"{localAppData}\Temp",
                $@"{localAppData}\CrashDumps",
                $@"{localAppData}\ConnectedDevicesPlatform",
                $@"{localAppData}\D3DSCache",
                $@"{localAppData}\ElevatedDiagnostics",
                $@"{localAppData}\GroupPolicy",
                $@"{localAppData}\NVIDIA",
                $@"{localAppData}\AMD",
                $@"{localAppData}\Intel",
                $@"{roaming}\Microsoft",
                environment.Temp,
            }),
            FileAllowPrefixes = Prune(new[]
            {
                $@"{programData}\Microsoft\Windows\Start Menu",
                $@"{roaming}\Microsoft\Windows\Start Menu",
            }),
            FileNameDenySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "desktop.ini",
                "thumbs.db",
                ".ds_store",
                "iconcache.db",
            },
        };

        static IReadOnlyList<string> Prune(IEnumerable<string> values) =>
            values
                .Select(PathTools.Normalize)
                .Where(v => v.Length > 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
