using System.IO.Enumeration;
using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Snapshot;

/// <summary>
/// A snapshot of the file system locations where applications leave data:
/// Program Files, ProgramData, per-user AppData, Start Menu and desktops.
/// </summary>
public sealed record FileSnapshot(IReadOnlySet<string> Directories, IReadOnlySet<string> Files)
{
    public int Count => Directories.Count + Files.Count;

    public static FileSnapshot Capture(
        IReadOnlyList<string> roots,
        LeftoverRules rules,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        int maxEntries = 4_000_000)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastReport = 0;

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                continue;
            }

            var enumerable = new FileSystemEnumerable<(string Path, bool IsDirectory)>(
                root,
                (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory),
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                    ReturnSpecialDirectories = false,
                })
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                    !rules.IsFileDenied(entry.ToFullPath()),
                ShouldRecursePredicate = (ref FileSystemEntry entry) =>
                    !entry.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    && rules.CanDescendIntoFile(entry.ToFullPath()),
            };

            foreach (var (path, isDirectory) in enumerable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (isDirectory)
                {
                    directories.Add(path);
                }
                else
                {
                    files.Add(path);
                }

                if (directories.Count + files.Count - lastReport >= 100_000)
                {
                    lastReport = directories.Count + files.Count;
                    progress?.Report($"Indexed {lastReport:N0} file system entries");
                }

                if (directories.Count + files.Count >= maxEntries)
                {
                    progress?.Report($"Stopped at the {maxEntries:N0} entry safety limit for {root}");
                    break;
                }
            }
        }

        progress?.Report($"File snapshot captured: {directories.Count:N0} folders, {files.Count:N0} files");
        return new FileSnapshot(directories, files);
    }
}
