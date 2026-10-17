using System.Diagnostics;
using Microsoft.Win32;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Execution;

/// <summary>Result of a fingerprint driven scan that does not depend on snapshots.</summary>
public sealed record StaticScanResult
{
    public required IReadOnlyList<LeftoverItem> Items { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public required TimeSpan Duration { get; init; }

    public long TotalSizeBytes { get; init; }

    public int RegistryCandidates { get; init; }

    public int FileCandidates { get; init; }

    public LeftoverScanResult ToScanResult() => new()
    {
        Items = Items,
        RegistryVanishedCount = 0,
        FilesVanishedCount = 0,
        Duration = Duration,
    };
}

/// <summary>
/// Finds leftovers without a before/after snapshot by walking only the places
/// the fingerprint points at: install roots, vendor/app folders under the usual
/// data roots, Start Menu and desktop shortcuts, and matching registry branches.
/// This is what makes "scan then remove" possible for apps that are already gone.
/// </summary>
public sealed class StaticScanner
{
    private const int MaxRegistryKeys = 600;
    private const int MaxRegistryDepth = 6;
    private const int MaxDirectoryEntries = 6_000;
    private const int MaxStartMenuDepth = 2;
    private const int MaxSizedItems = 300;

    private readonly FingerprintEnvironment _environment;
    private readonly LeftoverRules _rules;
    private readonly LeftoverClassifierOptions _options;

    public StaticScanner(LeftoverRules? rules = null, LeftoverClassifierOptions? options = null)
    {
        _environment = FingerprintEnvironment.Current;
        _rules = rules ?? LeftoverRules.CreateDefault(_environment);
        _options = options ?? new LeftoverClassifierOptions();
    }

    public StaticScanResult Scan(
        InstalledApp app,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        var fingerprint = FingerprintBuilder.From(app, _environment, CollectInstallRootFileNames(app.InstallLocation));
        var classifier = new LeftoverClassifier(fingerprint, _rules, _environment, _options);

        progress?.Report($"Fingerprint: {fingerprint.NameTokens.Count} name token(s), {fingerprint.InstallRoots.Count} install root(s)");

        var fileCandidates = CollectFileCandidates(fingerprint, progress, cancellationToken);
        var registryCandidates = CollectRegistryCandidates(fingerprint, progress, cancellationToken);

        progress?.Report($"Classifying {fileCandidates.Count + registryCandidates.Count} candidate(s)");

        var collected = new List<LeftoverItem>();
        foreach (var candidate in fileCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = classifier.ClassifyOne(candidate.Path, isRegistry: false, isDirectory: candidate.IsDirectory);
            if (item is not null)
            {
                collected.Add(item);
            }
        }

        foreach (var candidate in registryCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = classifier.ClassifyOne(candidate, isRegistry: true, isDirectory: true);
            if (item is not null)
            {
                collected.Add(item);
            }
        }

        var pruned = PruneNested(collected);
        ComputeSizes(pruned, progress);

        var sorted = pruned
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Tier)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new StaticScanResult
        {
            Items = sorted,
            Warnings = warnings,
            Duration = stopwatch.Elapsed,
            TotalSizeBytes = sorted.Sum(item => item.SizeBytes ?? 0),
            RegistryCandidates = registryCandidates.Count,
            FileCandidates = fileCandidates.Count,
        };
    }

    private List<(string Path, bool IsDirectory)> CollectFileCandidates(
        AppFingerprint fingerprint,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var seen = PathTools.CreatePathSet();
        var candidates = new List<(string, bool)>();

        void Add(string? path, bool isDirectory)
        {
            var normalized = PathTools.Normalize(path);
            if (normalized.Length < 4 || _rules.IsFileDenied(normalized) || !seen.Add(normalized))
            {
                return;
            }

            candidates.Add((normalized, isDirectory));
        }

        void AddDirectoryWithChildren(string directory, int childLimit)
        {
            Add(directory, isDirectory: true);
            if (candidates.Count >= MaxDirectoryEntries)
            {
                return;
            }

            try
            {
                var children = 0;
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (children++ >= childLimit || candidates.Count >= MaxDirectoryEntries)
                    {
                        break;
                    }

                    var isDirectory = Directory.Exists(entry);
                    Add(entry, isDirectory);
                }
            }
            catch (Exception)
            {
                // Unreadable folder: the entry itself is still a candidate.
            }
        }

        foreach (var root in fingerprint.InstallRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(root))
            {
                AddDirectoryWithChildren(root, childLimit: 40);
            }
            else if (File.Exists(root))
            {
                Add(root, isDirectory: false);
            }
        }

        foreach (var candidate in fingerprint.RootCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(candidate))
            {
                AddDirectoryWithChildren(candidate, childLimit: 40);
            }
        }

        AddShortcutCandidates(Add, progress, cancellationToken);

        return candidates;
    }

    private void AddShortcutCandidates(
        Action<string?, bool> add,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var menus = new[]
        {
            Path.Combine(_environment.ProgramData, "Microsoft", "Windows", "Start Menu", "Programs"),
            Path.Combine(_environment.RoamingAppData, "Microsoft", "Windows", "Start Menu", "Programs"),
        };

        foreach (var menu in menus)
        {
            if (!Directory.Exists(menu))
            {
                continue;
            }

            progress?.Report($"Checking {menu}");
            WalkShortcuts(menu, 0);
        }

        foreach (var desktop in new[] { _environment.PublicDesktop, _environment.UserDesktop })
        {
            if (!Directory.Exists(desktop))
            {
                continue;
            }

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(desktop))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        add(entry, false);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore an unreadable desktop.
            }
        }

        void WalkShortcuts(string directory, int depth)
        {
            if (depth > MaxStartMenuDepth)
            {
                return;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(entry))
                {
                    add(entry, true);
                    WalkShortcuts(entry, depth + 1);
                }
                else if (entry.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    add(entry, false);
                }
            }
        }
    }

    private List<string> CollectRegistryCandidates(
        AppFingerprint fingerprint,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var names = BuildCandidateNames(fingerprint);
        var keys = new List<string>();

        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var hive in new[] { @"HKLM\SOFTWARE", @"HKLM\SOFTWARE\WOW6432Node", @"HKCU\SOFTWARE", @"HKCU\SOFTWARE\WOW6432Node" })
            {
                var key = $@"{hive}\{name}";
                if (!KeyExists(key) || _rules.IsRegistryDenied(key))
                {
                    continue;
                }

                WalkKey(key, keys, cancellationToken);
                if (keys.Count >= MaxRegistryKeys)
                {
                    return keys;
                }
            }
        }

        progress?.Report($"Registry candidates: {keys.Count}");
        return keys;
    }

    private static List<string> BuildCandidateNames(AppFingerprint fingerprint)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? value)
        {
            var name = (value ?? string.Empty).Trim();
            if (name.Length < 2 || name.Length > 64 || !seen.Add(name))
            {
                return;
            }

            names.Add(name);
        }

        foreach (var folder in fingerprint.KnownFolderNames)
        {
            Add(folder);
        }

        foreach (var root in fingerprint.InstallRoots)
        {
            Add(PathTools.LastSegment(root));
        }

        foreach (var candidate in fingerprint.RootCandidates)
        {
            Add(PathTools.LastSegment(candidate));
        }

        if (!string.IsNullOrWhiteSpace(fingerprint.Publisher))
        {
            Add(fingerprint.Publisher);
        }

        Add(fingerprint.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal));
        Add(fingerprint.DisplayName);

        foreach (var token in fingerprint.VendorTokens)
        {
            Add(token);
        }

        foreach (var token in fingerprint.NameTokens)
        {
            Add(token);
        }

        return names;
    }

    private bool KeyExists(string canonicalPath)
    {
        var split = PathTools.SplitRegistryPath(canonicalPath);
        if (split is null)
        {
            return false;
        }

        var hive = MapHive(split.Value.Hive);
        if (hive is null)
        {
            return false;
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(split.Value.SubPath, writable: false);
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void WalkKey(string canonicalPath, List<string> sink, CancellationToken cancellationToken)
    {
        var split = PathTools.SplitRegistryPath(canonicalPath);
        if (split is null)
        {
            return;
        }

        var hive = MapHive(split.Value.Hive);
        if (hive is null)
        {
            return;
        }

        var queue = new Queue<(string Canonical, int Depth)>();
        queue.Enqueue((canonicalPath, 0));

        while (queue.Count > 0 && sink.Count < MaxRegistryKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, depth) = queue.Dequeue();

            var currentSplit = PathTools.SplitRegistryPath(current);
            if (currentSplit is null)
            {
                continue;
            }

            string[] children;
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(currentSplit.Value.SubPath, writable: false);
                if (key is null)
                {
                    continue;
                }

                children = key.GetSubKeyNames();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var child in children)
            {
                var childPath = $@"{current}\{child}";
                if (!_rules.CanDescendIntoRegistry(childPath))
                {
                    continue;
                }

                if (!_rules.IsRegistryDenied(childPath))
                {
                    sink.Add(childPath);
                    if (sink.Count >= MaxRegistryKeys)
                    {
                        return;
                    }
                }

                if (depth < MaxRegistryDepth)
                {
                    queue.Enqueue((childPath, depth + 1));
                }
            }
        }
    }

    private static List<LeftoverItem> PruneNested(List<LeftoverItem> items)
    {
        var containers = new HashSet<string>(
            items
                .Where(item => item.Kind != SystemItemKind.File)
                .Select(item => item.Path),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<LeftoverItem>(items.Count);
        foreach (var item in items)
        {
            var path = item.Path;
            var hasSelectedAncestor = false;

            while (true)
            {
                var parent = PathTools.Parent(path);
                if (parent.Length < 4)
                {
                    break;
                }

                if (containers.Contains(parent))
                {
                    hasSelectedAncestor = true;
                    break;
                }

                path = parent;
            }

            if (!hasSelectedAncestor)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static void ComputeSizes(List<LeftoverItem> items, IProgress<string>? progress)
    {
        var counted = 0;
        foreach (var item in items)
        {
            if (item.IsRegistry || counted >= MaxSizedItems)
            {
                continue;
            }

            item.SizeBytes = SizeCalculator.GetSize(item.Path);
            counted++;
        }

        if (counted > 0)
        {
            progress?.Report($"Sizes computed for {counted} item(s)");
        }
    }

    private static IReadOnlyList<string> CollectInstallRootFileNames(string? installLocation)
    {
        var root = PathTools.Normalize(installLocation);
        if (root.Length < 4 || !Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(64);
        try
        {
            foreach (var file in Directory.EnumerateFiles(root))
            {
                names.Add(Path.GetFileName(file));
                if (names.Count >= 2_000)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Unreadable install root: continue without file evidence.
        }

        return names;
    }

    private static RegistryHive? MapHive(string hive) => hive switch
    {
        "HKLM" => RegistryHive.LocalMachine,
        "HKCU" => RegistryHive.CurrentUser,
        "HKCR" => RegistryHive.ClassesRoot,
        "HKU" => RegistryHive.Users,
        _ => null,
    };
}
