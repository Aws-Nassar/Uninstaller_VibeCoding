using System.Text.RegularExpressions;

namespace VibeUninstaller.Core.Analysis;

/// <summary>
/// Splits display names, publishers and file names into significant words.
/// Version numbers, bitness and generic words are removed so that
/// "7-Zip 24.09 (x64)" and "7-Zip" produce the same token set.
/// </summary>
public static partial class NameTokenizer
{
    private const int MinTokenLength = 2;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // generic software words
        "app", "apps", "application", "applications", "program", "programs", "software", "suite", "tool", "tools",
        "utility", "utilities", "package", "packages", "component", "components", "module", "modules", "framework",
        "runtime", "runtimes", "library", "libraries", "sdk", "engine", "platform", "system", "systems", "service",
        "services", "driver", "drivers", "client", "server", "desktop", "console", "manager", "center", "centre",
        // install-ish words
        "setup", "install", "installer", "installation", "uninstall", "uninstaller", "update", "updates", "updater",
        "version", "ver", "release", "releases", "build", "edition", "oem", "retail",
        // editions / licensing
        "free", "pro", "proffesional", "professional", "premium", "plus", "premier", "ultimate", "deluxe", "express",
        "standard", "basic", "home", "business", "enterprise", "community", "portable", "light", "lite", "demo",
        "trial", "beta", "alpha", "preview", "stable", "classic", "complete", "full",
        // grammar
        "for", "the", "and", "with", "of", "to", "in", "on", "by", "at", "or", "a", "an", "from", "your",
        // platforms / bitness / locales
        "win", "windows", "window", "x86", "x64", "x32", "amd64", "arm64", "64bit", "32bit", "bit", "win32", "win64",
        "en", "us", "uk", "www", "com", "org", "net", "exe", "dll", "lnk", "ico", "msi", "msix", "appx",
        // file-ish noise
        "log", "logs", "txt", "ini", "cfg", "conf", "config", "configs", "settings", "dat", "data", "db", "tmp",
        "temp", "cache", "caches", "bak", "old", "new", "backup", "backups", "json", "xml", "png", "jpg", "jpeg",
        "bmp", "gif", "svg",
    };

    /// <summary>Words too generic to make a folder name a "certain" match.</summary>
    private static readonly HashSet<string> WeakFolderTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "lib", "libs", "src", "test", "tests", "docs", "doc", "documentation", "help", "locale",
        "locales", "lang", "langs", "language", "languages", "res", "resources", "assets", "images", "image",
        "icons", "icon", "themes", "theme", "skins", "skin", "fonts", "font", "plugins", "plugin", "addon",
        "addons", "extension", "extensions", "samples", "sample", "example", "examples", "template", "templates",
        "profile", "profiles", "default", "defaults", "main", "core", "common", "shared", "local", "roaming",
        "current", "user", "users", "userdata", "save", "saves", "savegame", "savedata", "media", "music", "sound",
        "sounds", "video", "videos", "movie", "movies", "texture", "textures", "shader", "shaders", "map", "maps",
        "level", "levels", "script", "scripts", "cmd", "screen", "output", "input", "work", "working", "export",
        "import", "report", "reports", "results", "mail", "email", "notes", "note", "projects", "project", "sync",
    };

    private static readonly HashSet<string> LegalSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "inc", "incorporated", "llc", "ltd", "limited", "corp", "corporation", "gmbh", "co", "company", "technologies",
        "technology", "technological", "software", "systems", "solutions", "group", "holdings", "international",
        "global", "studios", "studio", "team", "labs", "laboratories", "productions", "media", "digital", "sro",
        "sa", "bv", "ab", "oy", "as", "ag", "plc", "pty", "pte", "kg", "kft", "spa", "sp", "zoo", "the",
    };

    [GeneratedRegex(@"^v?\d+[a-z]?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionLikeRegex();

    /// <summary>Tokenizes free text into significant lowercase words.</summary>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>(4);
        var span = text.AsSpan();
        var start = -1;

        for (var i = 0; i <= span.Length; i++)
        {
            var isAlphaNumeric = i < span.Length && char.IsLetterOrDigit(span[i]);
            if (isAlphaNumeric)
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                AddToken(span.Slice(start, i - start), tokens);
                start = -1;
            }
        }

        return tokens;
    }

    /// <summary>Tokenizes a publisher name and strips legal suffixes.</summary>
    public static IReadOnlyList<string> TokenizePublisher(string? publisher)
    {
        var raw = Tokenize(publisher);
        if (raw.Count == 0)
        {
            return raw;
        }

        var filtered = new List<string>(raw.Count);
        foreach (var token in raw)
        {
            if (!LegalSuffixes.Contains(token))
            {
                filtered.Add(token);
            }
        }

        // Every word was a legal suffix ("The Company"): keep the raw tokens.
        return filtered.Count > 0 ? filtered : raw;
    }

    /// <summary>True when a token is long and specific enough to identify an app on its own.</summary>
    public static bool IsStrongToken(string token) =>
        token.Length >= 3 && !WeakFolderTokens.Contains(token);

    public static bool IsWeakToken(string token) => WeakFolderTokens.Contains(token);

    private static void AddToken(ReadOnlySpan<char> raw, List<string> tokens)
    {
        if (raw.Length < MinTokenLength)
        {
            return;
        }

        var allDigits = true;
        foreach (var character in raw)
        {
            if (!char.IsDigit(character))
            {
                allDigits = false;
                break;
            }
        }

        if (allDigits)
        {
            return;
        }

        var token = raw.ToString().ToLowerInvariant();
        if (StopWords.Contains(token) || VersionLikeRegex().IsMatch(token))
        {
            return;
        }

        tokens.Add(token);
    }
}
