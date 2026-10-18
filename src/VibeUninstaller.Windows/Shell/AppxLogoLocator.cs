using System.IO;
using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Windows.Shell;

/// <summary>
/// Finds the image a Store/MSIX package ships as its logo. The manifest names a
/// single file, but Windows generates scale and target-size variants next to
/// it, so the folder is searched for the largest matching image.
/// </summary>
public static class AppxLogoLocator
{
    private const string ManifestName = "AppxManifest.xml";
    private const string FallbackBaseName = "Square44x44Logo";

    /// <summary>Returns the path of the best logo image, or null when the package has none.</summary>
    public static string? Find(string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return null;
        }

        var root = Environment.ExpandEnvironmentVariables(installLocation.Trim().Trim('"'));
        if (!Directory.Exists(root))
        {
            return null;
        }

        var declared = ReadManifestLogo(root);
        var baseName = string.IsNullOrEmpty(declared)
            ? FallbackBaseName
            : Path.GetFileNameWithoutExtension(declared);

        var declaredFolder = string.IsNullOrEmpty(declared) ? null : Path.GetDirectoryName(declared);

        foreach (var folder in CandidateFolders(root, declaredFolder))
        {
            if (FindLargestLogo(folder, baseName) is { } logo)
            {
                return logo;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateFolders(string root, string? declaredFolder)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(declaredFolder))
        {
            var declared = SafeCombine(root, declaredFolder);
            if (declared is not null && seen.Add(declared))
            {
                yield return declared;
            }
        }

        if (seen.Add(root))
        {
            yield return root;
        }

        var assets = SafeCombine(root, "Assets");
        if (assets is not null && seen.Add(assets))
        {
            yield return assets;
        }
    }

    private static string? ReadManifestLogo(string root)
    {
        try
        {
            var manifest = Path.Combine(root, ManifestName);
            return File.Exists(manifest) ? AppxLogoParser.ParseLogoPath(File.ReadAllText(manifest)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FindLargestLogo(string folder, string baseName)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        try
        {
            string? best = null;
            var bestSize = -1L;

            foreach (var path in Directory.EnumerateFiles(folder, baseName + "*.png"))
            {
                long size;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (IOException)
                {
                    continue;
                }

                if (size > bestSize)
                {
                    bestSize = size;
                    best = path;
                }
            }

            return best;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? SafeCombine(string root, string child)
    {
        try
        {
            return Path.GetFullPath(Path.Combine(root, child));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
