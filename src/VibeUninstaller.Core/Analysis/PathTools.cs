using System.Text;

namespace VibeUninstaller.Core.Analysis;

/// <summary>Path helpers that work for both file system and canonical registry paths.</summary>
public static class PathTools
{
    private const char Separator = '\\';

    /// <summary>
    /// Normalizes a path: trims quotes/whitespace, expands environment variables,
    /// converts forward slashes and removes redundant trailing separators.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var value = path.Trim().Trim('"').Trim();
        value = Environment.ExpandEnvironmentVariables(value);
        value = value.Replace('/', Separator);

        if (!value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            while (value.Contains(@"\\", StringComparison.Ordinal))
            {
                value = value.Replace(@"\\", @"\", StringComparison.Ordinal);
            }
        }

        if (value.Length > 3 && value.EndsWith(Separator))
        {
            value = value.TrimEnd(Separator);
        }

        if (value.Length == 2 && value[1] == ':')
        {
            value += Separator;
        }

        return value;
    }

    /// <summary>True when <paramref name="candidate"/> is inside <paramref name="root"/>.</summary>
    public static bool IsUnder(string? candidate, string? root, bool includeEqual = false)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root))
        {
            return false;
        }

        if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return includeEqual;
        }

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (candidate.Length <= root.Length)
        {
            return false;
        }

        return root.EndsWith(Separator) || candidate[root.Length] == Separator;
    }

    /// <summary>Splits a path into segments, keeping the drive/UNC root out.</summary>
    public static string[] SplitSegments(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Array.Empty<string>();
        }

        var parts = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? Array.Empty<string>() : parts;
    }

    /// <summary>Returns the last segment of a path ("C:\A\B" =&gt; "B").</summary>
    public static string LastSegment(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd(Separator);
        var index = trimmed.LastIndexOf(Separator);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    /// <summary>Returns the parent path, or an empty string for roots.</summary>
    public static string Parent(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var trimmed = path.TrimEnd(Separator);
        var index = trimmed.LastIndexOf(Separator);
        if (index <= 1)
        {
            return string.Empty;
        }

        // "C:\A" keeps its drive root, so callers can walk upwards safely.
        if (index == 2 && trimmed.Length > 2 && trimmed[1] == ':')
        {
            return trimmed[..3];
        }

        return trimmed[..index];
    }

    public static (string Hive, string SubPath)? SplitRegistryPath(string? canonicalPath)
    {
        if (string.IsNullOrEmpty(canonicalPath))
        {
            return null;
        }

        var separator = canonicalPath.IndexOf(Separator);
        if (separator <= 0)
        {
            return null;
        }

        var hive = canonicalPath[..separator].ToUpperInvariant();
        var subPath = canonicalPath[(separator + 1)..];
        return hive is "HKLM" or "HKCU" or "HKCR" or "HKU" ? (hive, subPath) : null;
    }

    public static bool LooksLikeGuid(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(
            value, @"^\{?[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}?$");
    }

    /// <summary>
    /// Converts a dashed MSI product code to the "packed" form used in the registry.
    /// Example: 367FEF4F-3275-477E-8FA3-C8A61CE34ED5 becomes F4FEF7635723E774F83A8C6AC13EE45D.
    /// </summary>
    public static string? ToPackedProductCode(string? productCode)
    {
        if (string.IsNullOrEmpty(productCode))
        {
            return null;
        }

        var compact = productCode.Replace("{", string.Empty).Replace("}", string.Empty).Replace("-", string.Empty);
        if (compact.Length != 32)
        {
            return null;
        }

        var builder = new StringBuilder(32);
        AppendReversed(builder, compact.AsSpan(0, 8));
        AppendReversed(builder, compact.AsSpan(8, 4));
        AppendReversed(builder, compact.AsSpan(12, 4));
        for (var i = 16; i < 32; i += 2)
        {
            builder.Append(compact[i + 1]);
            builder.Append(compact[i]);
        }

        return builder.ToString().ToUpperInvariant();

        static void AppendReversed(StringBuilder builder, ReadOnlySpan<char> value)
        {
            for (var i = value.Length - 1; i >= 0; i--)
            {
                builder.Append(value[i]);
            }
        }
    }

    /// <summary>Creates a hash set with the Windows-typical case-insensitive comparison.</summary>
    public static HashSet<string> CreatePathSet() => new(StringComparer.OrdinalIgnoreCase);
}
