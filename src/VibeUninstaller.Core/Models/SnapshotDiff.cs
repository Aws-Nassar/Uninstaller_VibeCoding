namespace VibeUninstaller.Core.Models;

/// <summary>Which subsystem a candidate item lives in.</summary>
public enum SystemItemKind
{
    RegistryKey,
    File,
    Directory,
}

/// <summary>
/// Result of comparing a before snapshot with an after snapshot.
/// Sets keep the original casing of the "after" capture.
/// </summary>
public sealed record SnapshotDiff
{
    /// <summary>Registry keys that existed before and still exist after the uninstall.</summary>
    public required IReadOnlyCollection<string> RegistryRemaining { get; init; }

    /// <summary>Registry keys that did not exist before the uninstall.</summary>
    public required IReadOnlyCollection<string> RegistryCreated { get; init; }

    /// <summary>Directories that existed before and still exist after the uninstall.</summary>
    public required IReadOnlyCollection<string> DirectoriesRemaining { get; init; }

    /// <summary>Directories that did not exist before the uninstall.</summary>
    public required IReadOnlyCollection<string> DirectoriesCreated { get; init; }

    /// <summary>Files that existed before and still exist after the uninstall.</summary>
    public required IReadOnlyCollection<string> FilesRemaining { get; init; }

    /// <summary>Files that did not exist before the uninstall.</summary>
    public required IReadOnlyCollection<string> FilesCreated { get; init; }

    /// <summary>How many registry keys the uninstall removed (good sign, informational).</summary>
    public int RegistryVanishedCount { get; init; }

    /// <summary>How many file system entries the uninstall removed (good sign, informational).</summary>
    public int FilesVanishedCount { get; init; }

    public int CandidateCount =>
        RegistryRemaining.Count + RegistryCreated.Count +
        DirectoriesRemaining.Count + DirectoriesCreated.Count +
        FilesRemaining.Count + FilesCreated.Count;
}
