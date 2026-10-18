using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Core.Analysis;

/// <summary>Where an icon candidate comes from.</summary>
public enum IconSourceKind
{
    /// <summary>A file plus an icon index, taken from the installer's own entry.</summary>
    IconReference,

    /// <summary>The folder that holds the application; the executable inside is used.</summary>
    InstallLocation,
}

/// <summary>One place an icon may be found, with the order they should be tried in.</summary>
public sealed record IconCandidate(IconSourceKind Kind, string Path, int Index);

/// <summary>An executable found in an install folder.</summary>
public sealed record ExecutableFile(string Path, long SizeBytes, int Depth = 0);

/// <summary>
/// Works out, in preference order, where the icon of an application can come
/// from. It never touches the file system: the caller checks what exists, which
/// keeps the interesting rules testable.
/// </summary>
public static class IconResolver
{
    /// <summary>
    /// System binaries that are sometimes registered as DisplayIcon but never
    /// carry the application's icon.
    /// </summary>
    private static readonly HashSet<string> LauncherNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msiexec.exe",
        "rundll32.exe",
        "regsvr32.exe",
        "cmd.exe",
        "mshta.exe",
        "wscript.exe",
        "cscript.exe",
        "powershell.exe",
    };

    /// <summary>
    /// Names that mark a helper (installer, updater, crash handler) rather than
    /// the application itself.
    /// </summary>
    private static readonly string[] HelperFragments =
    {
        "unins",
        "uninstall",
        "unwise",
        "setup",
        "install",
        "update",
        "upgrade",
        "crash",
        "handler",
        "helper",
        "service",
        "elevate",
        "launcher",
        "loader",
        "watchdog",
        "report",
        "bootstrap",
        "vcredist",
        "dotnet",
        "licens",
        "readme",
    };

    /// <summary>The candidates for one application, best first. Paths may still contain environment variables.</summary>
    public static IReadOnlyList<IconCandidate> Candidates(InstalledApp app)
    {
        var candidates = new List<IconCandidate>(2);

        var reference = IconLocationParser.Parse(app.DisplayIcon);
        if (reference is not null)
        {
            var resolved = ResolveAgainstInstallLocation(reference.Path, app.InstallLocation);
            if (!IsLauncherBinary(resolved))
            {
                candidates.Add(new IconCandidate(IconSourceKind.IconReference, resolved, reference.Index));
            }
        }

        if (!string.IsNullOrWhiteSpace(app.InstallLocation))
        {
            candidates.Add(new IconCandidate(IconSourceKind.InstallLocation, app.InstallLocation.Trim(), 0));
        }

        return candidates;
    }

    /// <summary>
    /// A DisplayIcon value is often a bare file name that lives in the install
    /// folder, so it is combined with it. Environment variables are left alone.
    /// </summary>
    public static string ResolveAgainstInstallLocation(string path, string? installLocation)
    {
        var trimmed = path.Trim().Trim('"').Trim();
        if (trimmed.Length == 0 || IsRooted(trimmed) || string.IsNullOrWhiteSpace(installLocation))
        {
            return trimmed;
        }

        try
        {
            return Path.Combine(installLocation.Trim(), trimmed);
        }
        catch (ArgumentException)
        {
            return trimmed;
        }
    }

    /// <summary>True when the path is absolute; "%ProgramFiles%\..." counts as absolute too.</summary>
    public static bool IsRooted(string path) =>
        Path.IsPathRooted(path) || path.StartsWith('%') || path.StartsWith('{');

    public static bool IsLauncherBinary(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var name = Path.GetFileName(path.Trim().Trim('"'));
        return name.Length > 0 && LauncherNames.Contains(name);
    }

    /// <summary>True for installer/updater style binaries that should not represent the application.</summary>
    public static bool IsHelperExecutable(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        if (name.Length == 0 || LauncherNames.Contains(Path.GetFileName(fileName)))
        {
            return true;
        }

        return HelperFragments.Any(fragment => name.Contains(fragment, StringComparison.Ordinal));
    }

    /// <summary>
    /// Picks the executable that most likely represents the application: the
    /// one named after it, from its publisher, and reasonably large.
    /// </summary>
    public static string? PickExecutable(InstalledApp app, IEnumerable<ExecutableFile> files)
    {
        string? best = null;
        var bestScore = int.MinValue;

        foreach (var file in files)
        {
            var score = ScoreExecutable(app.DisplayName, app.Publisher, file.Path, file.SizeBytes, file.Depth);
            if (score > bestScore)
            {
                bestScore = score;
                best = file.Path;
            }
        }

        return bestScore == int.MinValue ? null : best;
    }

    /// <summary>
    /// Scores a file as "the application's main binary". Higher is better;
    /// <see cref="int.MinValue"/> means the file must not be used at all.
    /// </summary>
    public static int ScoreExecutable(string displayName, string? publisher, string fileName, long sizeBytes, int depth = 0)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (IsHelperExecutable(fileName))
        {
            return int.MinValue;
        }

        var normalized = Normalize(baseName);
        var normalizedName = Normalize(displayName);
        var score = 0;

        if (normalizedName.Length >= 3)
        {
            if (normalized == normalizedName)
            {
                score += 200;
            }
            else if (normalized.Length >= 3 && normalizedName.Contains(normalized, StringComparison.Ordinal))
            {
                score += 140;
            }
            else if (normalized.Contains(normalizedName, StringComparison.Ordinal))
            {
                score += 120;
            }
        }

        var tokenScore = 0;
        foreach (var token in Tokenize(displayName).Take(4))
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                tokenScore += 30;
            }
        }

        foreach (var token in Tokenize(publisher).Take(2))
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                tokenScore += 15;
            }
        }

        score += Math.Min(tokenScore, 90);

        if (normalized.StartsWith("app", StringComparison.Ordinal))
        {
            score += 40;
        }

        // A larger binary is more likely to be the program itself than a tool.
        score += (int)Math.Min(30, sizeBytes / (2L * 1024 * 1024));

        // The closer to the install root, the more likely it is the main binary.
        score -= depth * 15;

        return score;
    }

    private static IEnumerable<string> Tokenize(string? value) =>
        Normalize(value).Length == 0
            ? Array.Empty<string>()
            : Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length >= 3);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character is ' ' or '-' or '_' or '.')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }
}
