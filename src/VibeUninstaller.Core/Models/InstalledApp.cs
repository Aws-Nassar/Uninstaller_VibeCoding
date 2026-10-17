namespace VibeUninstaller.Core.Models;

/// <summary>Where an installed application was discovered.</summary>
public enum AppSource
{
    /// <summary>Classic Win32 application registered under the Uninstall registry keys.</summary>
    Registry,

    /// <summary>Microsoft Store / MSIX package.</summary>
    Appx,

    /// <summary>Steam game tracked through the Steam client.</summary>
    Steam,
}

/// <summary>Whether the application is installed for the whole machine or a single user.</summary>
public enum InstallScope
{
    Machine,
    User,
}

/// <summary>A single application discovered on the system.</summary>
public sealed record InstalledApp
{
    /// <summary>Stable identifier assembled from the discovery source.</summary>
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string? Version { get; init; }

    public string? Publisher { get; init; }

    /// <summary>Install root reported by the application, if any.</summary>
    public string? InstallLocation { get; init; }

    public string? UninstallString { get; init; }

    public string? QuietUninstallString { get; init; }

    /// <summary>Icon reference from the registry (usually "path,index").</summary>
    public string? DisplayIcon { get; init; }

    /// <summary>Size reported by the installer, in bytes. Often approximate or missing.</summary>
    public long? EstimatedSizeBytes { get; init; }

    public DateOnly? InstallDate { get; init; }

    /// <summary>MSI product code ({GUID}) when the application is installed by Windows Installer.</summary>
    public string? ProductCode { get; init; }

    /// <summary>Canonical path of the registry key the app was read from, if any.</summary>
    public string? RegistryKeyPath { get; init; }

    /// <summary>Full package name for MSIX/Store applications.</summary>
    public string? PackageFullName { get; init; }

    /// <summary>Steam application id.</summary>
    public string? SteamAppId { get; init; }

    public string? UrlInfoAbout { get; init; }

    public string? InstallSource { get; init; }

    public AppSource Source { get; init; } = AppSource.Registry;

    public InstallScope Scope { get; init; } = InstallScope.Machine;

    /// <summary>True for Windows update style entries ("Update for ...").</summary>
    public bool IsUpdate { get; init; }

    /// <summary>True for components the OS marks as system components.</summary>
    public bool IsSystemComponent { get; init; }

    /// <summary>Leftover scanning only makes sense for things installed on the file system/registry.</summary>
    public bool SupportsLeftoverScan => Source != AppSource.Appx;

    public string ScopeLabel => Scope == InstallScope.Machine ? "All users" : "Current user";

    public string SourceLabel => Source switch
    {
        AppSource.Appx => "Store",
        AppSource.Steam => "Steam",
        _ => Scope == InstallScope.Machine ? "System" : "User",
    };
}
