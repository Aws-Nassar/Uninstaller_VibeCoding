namespace VibeUninstaller.Core.Models;

/// <summary>Filters applied before a user supplied query is matched.</summary>
public sealed record AppMatchOptions
{
    /// <summary>Include entries Windows marks as system components or updates.</summary>
    public bool IncludeSystem { get; init; }

    /// <summary>Include Store (MSIX) packages.</summary>
    public bool IncludeStore { get; init; } = true;

    /// <summary>Include Steam games.</summary>
    public bool IncludeSteam { get; init; } = true;

    public static AppMatchOptions FromFlags(bool includeSystem, bool includeStore, bool includeSteam) => new()
    {
        IncludeSystem = includeSystem,
        IncludeStore = includeStore,
        IncludeSteam = includeSteam,
    };
}

public enum AppMatchKind
{
    /// <summary>Nothing matched the query.</summary>
    None,

    /// <summary>Exactly one application matched.</summary>
    Single,

    /// <summary>Several applications matched; the caller must ask for more detail.</summary>
    Ambiguous,
}

public sealed record AppMatchResult
{
    public required AppMatchKind Kind { get; init; }

    public InstalledApp? App { get; init; }

    public IReadOnlyList<InstalledApp> Candidates { get; init; } = Array.Empty<InstalledApp>();

    public string? Message { get; init; }

    public static AppMatchResult Single(InstalledApp app) => new() { Kind = AppMatchKind.Single, App = app };

    public static AppMatchResult None(string message, IReadOnlyList<InstalledApp>? near = null) => new()
    {
        Kind = AppMatchKind.None,
        Message = message,
        Candidates = near ?? Array.Empty<InstalledApp>(),
    };

    public static AppMatchResult Ambiguous(IReadOnlyList<InstalledApp> candidates) => new()
    {
        Kind = AppMatchKind.Ambiguous,
        Candidates = candidates,
        Message = $"{candidates.Count} applications match the query.",
    };
}

/// <summary>
/// Turns a user supplied string (CLI argument or search box content) into one
/// application. Pure logic, so the rules are identical for the GUI and the CLI
/// and can be unit tested.
/// </summary>
public static class AppMatcher
{
    public static AppMatchOptions Default { get; } = new();

    public static IReadOnlyList<InstalledApp> Filter(
        IReadOnlyList<InstalledApp> apps,
        AppMatchOptions? options = null)
    {
        var settings = options ?? Default;

        return apps
            .Where(app => settings.IncludeSystem || (!app.IsUpdate && !app.IsSystemComponent))
            .Where(app => settings.IncludeStore || app.Source != AppSource.Appx)
            .Where(app => settings.IncludeSteam || app.Source != AppSource.Steam)
            .ToArray();
    }

    public static AppMatchResult Match(
        IReadOnlyList<InstalledApp> apps,
        string query,
        AppMatchOptions? options = null)
    {
        var pool = Filter(apps, options);
        var needle = (query ?? string.Empty).Trim();

        if (needle.Length == 0)
        {
            return AppMatchResult.None("The application name must not be empty.");
        }

        var byId = pool.FirstOrDefault(app => app.Id.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return AppMatchResult.Single(byId);
        }

        var exact = pool
            .Where(app => app.DisplayName.Trim().Equals(needle, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (exact.Length == 1)
        {
            return AppMatchResult.Single(exact[0]);
        }

        if (exact.Length > 1)
        {
            return AppMatchResult.Ambiguous(Sort(exact));
        }

        var contains = pool
            .Where(app => app.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (contains.Length == 1)
        {
            return AppMatchResult.Single(contains[0]);
        }

        if (contains.Length > 1)
        {
            return AppMatchResult.Ambiguous(Sort(contains));
        }

        // Last resort: every word of the query must appear as a word of the name,
        // which copes with punctuation and word order differences.
        var tokenMatches = pool
            .Where(app => AllWordsPresent(app.DisplayName, needle))
            .ToArray();

        if (tokenMatches.Length == 1)
        {
            return AppMatchResult.Single(tokenMatches[0]);
        }

        if (tokenMatches.Length > 1)
        {
            return AppMatchResult.Ambiguous(Sort(tokenMatches));
        }

        var near = pool
            .Where(app => SharesFirstWord(app.DisplayName, needle))
            .Take(8)
            .ToArray();

        return AppMatchResult.None($"No installed application matches '{needle}'.", Sort(near));
    }

    private static bool AllWordsPresent(string displayName, string query)
    {
        var words = SplitIntoWords(query);
        if (words.Length == 0)
        {
            return false;
        }

        foreach (var word in words)
        {
            if (!displayName.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SharesFirstWord(string displayName, string query)
    {
        var words = SplitIntoWords(query);
        if (words.Length == 0)
        {
            return false;
        }

        var first = words[0];
        return first.Length >= 3 && displayName.Contains(first, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] SplitIntoWords(string value) =>
        value.Split(new[] { ' ', '\t', '-', '_', '.', ',', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<InstalledApp> Sort(IEnumerable<InstalledApp> apps) => apps
        .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(app => app.Version, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
