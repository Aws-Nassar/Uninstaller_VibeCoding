namespace VibeUninstaller.Core.Models;

/// <summary>
/// A normalized description of an installed application, used to decide which
/// leftovers belong to it. Built before the app is uninstalled.
/// </summary>
public sealed record AppFingerprint
{
    public required string DisplayName { get; init; }

    /// <summary>Significant words of the display name (versions/editions removed).</summary>
    public required IReadOnlySet<string> NameTokens { get; init; }

    /// <summary>Significant words of the publisher name (legal suffixes removed).</summary>
    public required IReadOnlySet<string> VendorTokens { get; init; }

    /// <summary>Known install roots (from InstallLocation and the uninstaller path).</summary>
    public required IReadOnlyList<string> InstallRoots { get; init; }

    /// <summary>Plausible per-vendor/per-app folders under ProgramData / AppData / Program Files.</summary>
    public required IReadOnlyList<string> RootCandidates { get; init; }

    /// <summary>Folder names that are known to belong to the app (lowercase).</summary>
    public required IReadOnlySet<string> KnownFolderNames { get; init; }

    /// <summary>File names found inside the install root (lowercase, e.g. 7zfm.exe).</summary>
    public IReadOnlySet<string> KnownFileNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? Publisher { get; init; }

    public string? ProductCode { get; init; }

    /// <summary>MSI "packed" product code as used under Installer\Products.</summary>
    public string? PackedProductCode { get; init; }

    public string? UninstallerPath { get; init; }

    public DateOnly? InstallDate { get; init; }

    public bool HasNameEvidence => NameTokens.Count > 0;

    public bool HasVendorEvidence => VendorTokens.Count > 0;

    public static AppFingerprint Minimal(string displayName) => new()
    {
        DisplayName = displayName,
        NameTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        VendorTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        InstallRoots = Array.Empty<string>(),
        RootCandidates = Array.Empty<string>(),
        KnownFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    };
}
