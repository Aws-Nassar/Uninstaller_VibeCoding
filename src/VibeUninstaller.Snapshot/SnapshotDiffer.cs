using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Snapshot;

/// <summary>Compares two snapshots and produces the candidate sets for the classifier.</summary>
public static class SnapshotDiffer
{
    public static SnapshotDiff Diff(
        RegistrySnapshot before,
        RegistrySnapshot after,
        FileSnapshot beforeFiles,
        FileSnapshot afterFiles,
        CancellationToken cancellationToken = default)
    {
        var registryRemaining = new List<string>();
        var registryCreated = new List<string>();
        foreach (var key in after.KeyPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (before.KeyPaths.Contains(key))
            {
                registryRemaining.Add(key);
            }
            else
            {
                registryCreated.Add(key);
            }
        }

        var directoriesRemaining = new List<string>();
        var directoriesCreated = new List<string>();
        Split(afterFiles.Directories, beforeFiles.Directories, directoriesRemaining, directoriesCreated, cancellationToken);

        var filesRemaining = new List<string>();
        var filesCreated = new List<string>();
        Split(afterFiles.Files, beforeFiles.Files, filesRemaining, filesCreated, cancellationToken);

        var registryVanished = before.KeyPaths.Count - registryRemaining.Count;
        var filesVanished =
            beforeFiles.Directories.Count - directoriesRemaining.Count +
            beforeFiles.Files.Count - filesRemaining.Count;

        return new SnapshotDiff
        {
            RegistryRemaining = registryRemaining,
            RegistryCreated = registryCreated,
            DirectoriesRemaining = directoriesRemaining,
            DirectoriesCreated = directoriesCreated,
            FilesRemaining = filesRemaining,
            FilesCreated = filesCreated,
            RegistryVanishedCount = Math.Max(0, registryVanished),
            FilesVanishedCount = Math.Max(0, filesVanished),
        };
    }

    private static void Split(
        IReadOnlySet<string> after,
        IReadOnlySet<string> before,
        List<string> remaining,
        List<string> created,
        CancellationToken cancellationToken)
    {
        foreach (var path in after)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (before.Contains(path))
            {
                remaining.Add(path);
            }
            else
            {
                created.Add(path);
            }
        }
    }
}
