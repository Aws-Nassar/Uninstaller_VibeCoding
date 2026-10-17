using Microsoft.Win32;
using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Snapshot;

/// <summary>
/// A compact snapshot of the registry subtrees that matter for uninstalls.
/// Only key paths are stored; value level analysis happens later, targeted,
/// on the few keys that are actually candidates.
/// </summary>
public sealed record RegistrySnapshot(IReadOnlySet<string> KeyPaths)
{
    public int Count => KeyPaths.Count;

    public static RegistrySnapshot Capture(
        LeftoverRules rules,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var collected = new List<string>(256_000);
        var sync = new object();
        var roots = rules.RegistryRoots;

        foreach (var rootSpec in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var split = PathTools.SplitRegistryPath(rootSpec);
            if (split is null)
            {
                continue;
            }

            var hive = MapHive(split.Value.Hive);
            if (hive is null)
            {
                continue;
            }

            using var baseKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
            using var rootKey = baseKey.OpenSubKey(split.Value.SubPath, writable: false);
            if (rootKey is null)
            {
                continue;
            }

            if (!rules.IsRegistryDenied(rootSpec))
            {
                collected.Add(rootSpec);
            }

            var topLevelNames = rootKey.GetSubKeyNames();
            progress?.Report($"Scanning {rootSpec} ({topLevelNames.Length} branches)");

            Parallel.ForEach(
                topLevelNames,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
                },
                () => new List<string>(2048),
                (name, _, local) =>
                {
                    using var branchBase = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
                    using var branchRoot = branchBase.OpenSubKey(split.Value.SubPath, writable: false);
                    if (branchRoot is null)
                    {
                        return local;
                    }

                    using var child = branchRoot.OpenSubKey(name, writable: false);
                    if (child is null)
                    {
                        return local;
                    }

                    var canonical = $"{rootSpec}\\{name}";
                    if (!rules.CanDescendIntoRegistry(canonical))
                    {
                        return local;
                    }

                    if (!rules.IsRegistryDenied(canonical))
                    {
                        local.Add(canonical);
                    }

                    Walk(child, canonical, rules, local, cancellationToken);
                    return local;
                },
                local =>
                {
                    lock (sync)
                    {
                        collected.AddRange(local);
                    }
                });
        }

        cancellationToken.ThrowIfCancellationRequested();

        var set = new HashSet<string>(collected, StringComparer.OrdinalIgnoreCase);
        progress?.Report($"Registry snapshot captured: {set.Count:N0} keys");
        return new RegistrySnapshot(set);
    }

    private static void Walk(
        RegistryKey key,
        string canonical,
        LeftoverRules rules,
        List<string> sink,
        CancellationToken cancellationToken)
    {
        var stack = new Stack<(RegistryKey Key, string Canonical)>();
        stack.Push((key, canonical));

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, currentCanonical) = stack.Pop();

            string[] names;
            try
            {
                names = current.GetSubKeyNames();
            }
            catch (Exception)
            {
                current.Dispose();
                continue;
            }

            foreach (var name in names)
            {
                RegistryKey? child = null;
                try
                {
                    child = current.OpenSubKey(name, writable: false);
                }
                catch (Exception)
                {
                    // Access denied or the key vanished: ignore this branch.
                }

                if (child is null)
                {
                    continue;
                }

                var childCanonical = $"{currentCanonical}\\{name}";
                if (!rules.CanDescendIntoRegistry(childCanonical))
                {
                    child.Dispose();
                    continue;
                }

                if (!rules.IsRegistryDenied(childCanonical))
                {
                    sink.Add(childCanonical);
                }

                stack.Push((child, childCanonical));
            }

            current.Dispose();
        }
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
