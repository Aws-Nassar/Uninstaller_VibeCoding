using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Core.Analysis;

/// <summary>
/// Builds the fingerprint that is later used to recognize what belongs to an app.
/// Everything here is pure logic so it can be unit tested.
/// </summary>
public static class FingerprintBuilder
{
    public static AppFingerprint From(
        InstalledApp app,
        FingerprintEnvironment environment,
        IReadOnlyCollection<string>? installRootFileNames = null)
    {
        var nameTokens = ToSet(NameTokenizer.Tokenize(app.DisplayName));
        if (nameTokens.Count == 0)
        {
            nameTokens = ToSet(NameTokenizer.Tokenize(CompactName(app.DisplayName)));
        }

        var vendorTokens = ToSet(NameTokenizer.TokenizePublisher(app.Publisher));

        var standardRoots = environment.SnapshotRoots();
        var installRoots = new List<string>();
        var uninstallerPath = default(string);

        AddRoot(app.InstallLocation);

        var uninstallerExe = CommandLineSplitter.TryGetExecutablePath(app.UninstallString)
            ?? CommandLineSplitter.TryGetExecutablePath(app.QuietUninstallString);
        if (uninstallerExe is not null)
        {
            uninstallerPath = PathTools.Normalize(uninstallerExe);
            AddRoot(PathTools.Parent(uninstallerPath));
        }

        var iconPath = NormalizeIconPath(app.DisplayIcon);
        if (iconPath is not null)
        {
            AddRoot(PathTools.Parent(iconPath));
        }

        var knownFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in installRoots)
        {
            AddFolderName(PathTools.LastSegment(root));
        }

        if (uninstallerPath is not null)
        {
            AddFolderName(PathTools.LastSegment(PathTools.Parent(uninstallerPath)));
        }

        var rootCandidates = BuildRootCandidates(environment, installRoots, vendorTokens, knownFolderNames);

        var knownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (installRootFileNames is not null)
        {
            foreach (var file in installRootFileNames)
            {
                var name = PathTools.LastSegment(file);
                if (name.Length > 0)
                {
                    knownFiles.Add(name);
                }
            }
        }

        if (uninstallerPath is not null)
        {
            knownFiles.Add(PathTools.LastSegment(uninstallerPath));
        }

        if (iconPath is not null && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            knownFiles.Add(PathTools.LastSegment(iconPath));
        }

        var productCode = NormalizeProductCode(app.ProductCode);

        return new AppFingerprint
        {
            DisplayName = app.DisplayName,
            NameTokens = nameTokens,
            VendorTokens = vendorTokens,
            InstallRoots = installRoots,
            RootCandidates = rootCandidates,
            KnownFolderNames = knownFolderNames,
            KnownFileNames = knownFiles,
            Publisher = app.Publisher,
            ProductCode = productCode,
            PackedProductCode = PathTools.ToPackedProductCode(productCode),
            UninstallerPath = uninstallerPath,
            InstallDate = app.InstallDate,
        };

        void AddRoot(string? candidate)
        {
            var root = PathTools.Normalize(candidate);
            if (!IsPlausibleRoot(root, environment, standardRoots))
            {
                return;
            }

            if (!installRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
            {
                installRoots.Add(root);
            }
        }

        void AddFolderName(string folder)
        {
            var name = folder.Trim().ToLowerInvariant();
            if (name.Length < 2 || folder.Length == 0)
            {
                return;
            }

            knownFolderNames.Add(name);
        }
    }

    /// <summary>A root must be specific enough to not swallow unrelated software.</summary>
    public static bool IsPlausibleRoot(string root, FingerprintEnvironment environment, IReadOnlyList<string>? standardRoots = null)
    {
        if (string.IsNullOrWhiteSpace(root) || root.Length < 4)
        {
            return false;
        }

        if (PathTools.IsUnder(root, environment.WindowsDirectory, includeEqual: true)
            || PathTools.IsUnder(root, environment.Temp, includeEqual: true))
        {
            return false;
        }

        var segments = PathTools.SplitSegments(root);
        if (segments.Length < 2)
        {
            return false;
        }

        foreach (var standard in standardRoots ?? environment.SnapshotRoots())
        {
            if (root.Equals(standard, StringComparison.OrdinalIgnoreCase)
                || PathTools.IsUnder(standard, root))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> BuildRootCandidates(
        FingerprintEnvironment environment,
        IReadOnlyList<string> installRoots,
        IReadOnlySet<string> vendorTokens,
        IReadOnlySet<string> knownFolderNames)
    {
        var bases = new[]
        {
            environment.ProgramData,
            environment.LocalAppData,
            environment.RoamingAppData,
            environment.LocalAppDataLow,
            environment.ProgramFiles,
            environment.ProgramFilesX86,
        };

        var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in vendorTokens)
        {
            folderNames.Add(token);
        }

        if (vendorTokens.Count > 1)
        {
            folderNames.Add(string.Join(' ', vendorTokens));
        }

        foreach (var name in knownFolderNames)
        {
            folderNames.Add(name);
        }

        foreach (var root in installRoots)
        {
            var parent = PathTools.Parent(root);
            if (parent.Length == 0)
            {
                continue;
            }

            var directChildOfBase = bases.Any(b => IsDirectChild(parent, b));
            if (directChildOfBase && NameTokenizer.Tokenize(PathTools.LastSegment(parent)).Count > 0)
            {
                folderNames.Add(PathTools.LastSegment(parent));
            }
        }

        var candidates = new List<string>();
        foreach (var basePath in bases)
        {
            foreach (var folder in folderNames)
            {
                if (folder.Length < 3 || NameTokenizer.Tokenize(folder).Count == 0)
                {
                    continue;
                }

                var candidate = PathTools.Normalize(Path.Combine(basePath, folder));
                if (!candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates;
    }

    private static bool IsDirectChild(string path, string parent)
    {
        if (!PathTools.IsUnder(path, parent))
        {
            return false;
        }

        return PathTools.Parent(path).Equals(parent.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var icon = displayIcon.Trim();
        var commaIndex = icon.LastIndexOf(',');
        if (commaIndex > 2)
        {
            icon = icon[..commaIndex];
        }

        icon = PathTools.Normalize(icon);
        return icon.Length == 0 ? null : icon;
    }

    private static string? NormalizeProductCode(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode) || !PathTools.LooksLikeGuid(productCode))
        {
            return null;
        }

        return productCode.Trim().ToUpperInvariant();
    }

    private static IReadOnlySet<string> ToSet(IReadOnlyList<string> tokens) =>
        tokens.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);

    private static string CompactName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return string.Empty;
        }

        return displayName
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
    }
}
