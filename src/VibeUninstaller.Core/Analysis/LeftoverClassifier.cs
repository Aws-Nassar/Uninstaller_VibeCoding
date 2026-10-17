using System.Diagnostics;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Core.Analysis;

public sealed record LeftoverClassifierOptions
{
    public double SafeThreshold { get; init; } = 5.0;

    public double ModerateThreshold { get; init; } = 2.5;

    public double AdvancedThreshold { get; init; } = 1.5;

    public bool IncludeAdvanced { get; init; } = true;

    public int MaxSegmentsToInspect { get; init; } = 6;
}

/// <summary>
/// Scores candidate items that remained after an uninstall against the app
/// fingerprint. Everything is evidence based: each detector contributes points
/// and a human readable reason, and the tier decides how confidently an item
/// may be offered for removal.
/// </summary>
public sealed class LeftoverClassifier
{
    private readonly AppFingerprint _fingerprint;
    private readonly LeftoverRules _rules;
    private readonly LeftoverClassifierOptions _options;
    private readonly IReadOnlyList<string> _standardRoots;
    private readonly IReadOnlyList<string> _prefilterTokens;
    private readonly string? _productCodeCompact;

    public LeftoverClassifier(
        AppFingerprint fingerprint,
        LeftoverRules rules,
        FingerprintEnvironment environment,
        LeftoverClassifierOptions? options = null)
    {
        _fingerprint = fingerprint;
        _rules = rules;
        _options = options ?? new LeftoverClassifierOptions();
        _standardRoots = environment.SnapshotRoots();

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        tokens.UnionWith(fingerprint.NameTokens);
        tokens.UnionWith(fingerprint.VendorTokens);
        tokens.UnionWith(fingerprint.KnownFolderNames);
        tokens.UnionWith(fingerprint.KnownFileNames);
        _prefilterTokens = tokens.Where(t => t.Length >= 2).ToArray();

        _productCodeCompact = fingerprint.ProductCode?.Replace("{", string.Empty).Replace("}", string.Empty);
    }

    public LeftoverScanResult Classify(SnapshotDiff diff, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var items = new List<LeftoverItem>();

        Consider(diff.RegistryRemaining, isRegistry: true, isDirectory: true, created: false, items, cancellationToken);
        Consider(diff.RegistryCreated, isRegistry: true, isDirectory: true, created: true, items, cancellationToken);
        Consider(diff.DirectoriesRemaining, isRegistry: false, isDirectory: true, created: false, items, cancellationToken);
        Consider(diff.DirectoriesCreated, isRegistry: false, isDirectory: true, created: true, items, cancellationToken);
        Consider(diff.FilesRemaining, isRegistry: false, isDirectory: false, created: false, items, cancellationToken);
        Consider(diff.FilesCreated, isRegistry: false, isDirectory: false, created: true, items, cancellationToken);

        var pruned = PruneNested(items);
        var sorted = pruned
            .OrderBy(i => i.Kind)
            .ThenByDescending(i => i.Score)
            .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LeftoverScanResult
        {
            Items = sorted,
            RegistryVanishedCount = diff.RegistryVanishedCount,
            FilesVanishedCount = diff.FilesVanishedCount,
            Duration = stopwatch.Elapsed,
        };
    }

    public LeftoverItem? ClassifyOne(string path, bool isRegistry, bool isDirectory, bool createdDuringUninstall = false)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (isRegistry ? _rules.IsRegistryDenied(path) : _rules.IsFileDenied(path))
        {
            return null;
        }

        if (IsProtectedContainer(path, isRegistry))
        {
            return null;
        }

        if (!MightMatch(path))
        {
            return null;
        }

        var segments = PathTools.SplitSegments(path);
        if (segments.Length == 0)
        {
            return null;
        }

        var start = ComputeStart(segments, path, isRegistry);
        if (start >= segments.Length)
        {
            return null;
        }

        var points = 0.0;
        var reasons = new List<string>(3);
        var hasName = false;
        var hasFullName = false;
        var hasProductCode = false;
        var hasContainer = false;

        foreach (var root in _fingerprint.InstallRoots)
        {
            if (PathTools.IsUnder(path, root))
            {
                points += 5;
                reasons.Add("Inside the app's install folder");
                hasContainer = true;
                hasFullName = true;
                break;
            }
        }

        if (!isRegistry)
        {
            foreach (var candidate in _fingerprint.RootCandidates)
            {
                if (PathTools.IsUnder(path, candidate))
                {
                    points += 3;
                    reasons.Add("Under a folder named after the app or publisher");
                    hasContainer = true;
                    break;
                }
            }

            if (_fingerprint.UninstallerPath is not null
                && path.Equals(_fingerprint.UninstallerPath, StringComparison.OrdinalIgnoreCase))
            {
                points += 6;
                reasons.Add("This is the app's own uninstaller");
                hasFullName = true;
            }
        }

        var leadingCount = Math.Min(2, segments.Length - start);
        var inspected = 0;
        for (var i = start; i < segments.Length && inspected < _options.MaxSegmentsToInspect; i++, inspected++)
        {
            var segment = segments[i];
            var leading = i - start < leadingCount;

            if (isRegistry && MatchesProductCode(segment))
            {
                points += 6;
                reasons.Add("Product code matches the app");
                hasProductCode = true;
                continue;
            }

            var lower = segment.ToLowerInvariant();
            if (_fingerprint.KnownFolderNames.Contains(lower))
            {
                points += leading ? 5 : 3;
                reasons.Add("Folder name matches the app");
                hasName = true;
                hasFullName = true;
                continue;
            }

            var tokens = NameTokenizer.Tokenize(lower);
            if (tokens.Count == 0)
            {
                continue;
            }

            var strong = tokens.Where(NameTokenizer.IsStrongToken).ToArray();
            if (strong.Length == 0)
            {
                continue;
            }

            var nameHits = strong.Count(_fingerprint.NameTokens.Contains);
            var vendorHits = strong.Count(_fingerprint.VendorTokens.Contains);

            if (nameHits > 0)
            {
                var coverage = (double)nameHits / strong.Length;

                // A single very short word ("zip") is too generic to be a full
                // match; it only counts as partial evidence.
                var isSingleShortWord = strong.Length == 1 && strong[0].Length <= 3;

                if (coverage >= 0.999 && !isSingleShortWord)
                {
                    if (strong.Length >= 2)
                    {
                        points += leading ? 4 : 3;
                        hasFullName = true;
                    }
                    else
                    {
                        points += leading ? 3 : 2;
                    }

                    hasName = true;
                    reasons.Add("Name matches the app");
                }
                else if (coverage >= 0.5)
                {
                    points += leading ? 2 : 1;
                    hasName = true;
                    reasons.Add("Name partly matches the app");
                }
            }
            else if (vendorHits > 0)
            {
                var coverage = (double)vendorHits / strong.Length;
                if (coverage >= 0.999)
                {
                    points += leading ? 3 : 1;
                    reasons.Add("Publisher matches");
                }
                else if (coverage >= 0.5)
                {
                    points += 1;
                    reasons.Add("Partly matches the publisher");
                }
            }
        }

        if (!isRegistry && !isDirectory && !hasContainer)
        {
            var fileName = PathTools.LastSegment(path);
            if (_fingerprint.KnownFileNames.Contains(fileName))
            {
                points += 4;
                reasons.Add("File belongs to the app");
                hasName = true;
            }
        }

        if (createdDuringUninstall && points >= 1)
        {
            points += 1;
            reasons.Add("Created while the uninstaller ran");
        }

        RiskTier? tier = null;
        if (points >= _options.SafeThreshold && (hasFullName || hasProductCode))
        {
            tier = RiskTier.Safe;
        }
        else if (points >= _options.ModerateThreshold && (hasName || (hasContainer && points >= 4)))
        {
            tier = RiskTier.Moderate;
        }
        else if (_options.IncludeAdvanced && points >= _options.AdvancedThreshold)
        {
            tier = RiskTier.Advanced;
        }

        if (tier is null)
        {
            return null;
        }

        var kind = isRegistry
            ? SystemItemKind.RegistryKey
            : isDirectory ? SystemItemKind.Directory : SystemItemKind.File;

        return new LeftoverItem
        {
            Path = path,
            Kind = kind,
            Tier = tier.Value,
            Score = Math.Min(1.0, points / 6.0),
            Reason = string.Join(" · ", reasons.Distinct().Take(3)),
            CreatedDuringUninstall = createdDuringUninstall,
        };
    }

    private void Consider(
        IReadOnlyCollection<string> paths,
        bool isRegistry,
        bool isDirectory,
        bool created,
        List<LeftoverItem> items,
        CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = ClassifyOne(path, isRegistry, isDirectory, created);
            if (item is not null)
            {
                items.Add(item);
            }
        }
    }

    private bool MightMatch(string path)
    {
        foreach (var root in _fingerprint.InstallRoots)
        {
            if (PathTools.IsUnder(path, root))
            {
                return true;
            }
        }

        foreach (var candidate in _fingerprint.RootCandidates)
        {
            if (PathTools.IsUnder(path, candidate))
            {
                return true;
            }
        }

        foreach (var token in _prefilterTokens)
        {
            if (path.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (_productCodeCompact is not null
            && path.Contains(_productCodeCompact, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool MatchesProductCode(string segment)
    {
        var code = _fingerprint.ProductCode;
        if (code is null)
        {
            return false;
        }

        if (segment.Equals(code, StringComparison.OrdinalIgnoreCase)
            || segment.Equals(code.Trim('{', '}'), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var packed = _fingerprint.PackedProductCode;
        return packed is not null && segment.Equals(packed, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsProtectedContainer(string path, bool isRegistry)
    {
        if (isRegistry)
        {
            foreach (var root in _rules.RegistryRoots)
            {
                if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var root in _standardRoots)
        {
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || PathTools.IsUnder(root, path))
            {
                return true;
            }
        }

        return false;
    }

    private int ComputeStart(string[] segments, string path, bool isRegistry)
    {
        if (isRegistry)
        {
            var start = 1;
            if (segments.Length > 1 && segments[1].Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase))
            {
                start = 2;
            }

            if (start < segments.Length && segments[start].Equals("WOW6432Node", StringComparison.OrdinalIgnoreCase))
            {
                start++;
            }

            return Math.Min(start, segments.Length);
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return Math.Min(2, segments.Length);
        }

        var best = 0;
        foreach (var root in _standardRoots)
        {
            if (PathTools.IsUnder(path, root, includeEqual: true))
            {
                var count = PathTools.SplitSegments(root).Length;
                if (count > best)
                {
                    best = count;
                }
            }
        }

        return best > 0 ? Math.Min(best, segments.Length) : Math.Min(1, segments.Length);
    }

    private static IReadOnlyList<LeftoverItem> PruneNested(List<LeftoverItem> items)
    {
        var containers = new HashSet<string>(
            items.Where(i => i.Kind != SystemItemKind.File).Select(i => i.Path),
            StringComparer.OrdinalIgnoreCase);

        if (containers.Count == 0)
        {
            return items;
        }

        var result = new List<LeftoverItem>(items.Count);
        foreach (var item in items)
        {
            if (item.Kind != SystemItemKind.File && HasSelectedAncestor(item.Path, containers))
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static bool HasSelectedAncestor(string path, HashSet<string> containers)
    {
        var current = PathTools.Parent(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (containers.Contains(current))
            {
                return true;
            }

            var next = PathTools.Parent(current);
            if (next.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = next;
        }

        return false;
    }
}
