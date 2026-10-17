namespace VibeUninstaller.Cli;

/// <summary>
/// Minimal, dependency free option parser. Each command declares which options
/// take a value, so "--timeout 30" is unambiguous and unknown options fail loudly.
/// </summary>
public sealed class CliOptions
{
    private static readonly string[] NoValueOptions = Array.Empty<string>();

    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = new();

    private CliOptions(string command)
    {
        Command = command;
    }

    public string Command { get; }

    /// <summary>Names that were given, for validation of mutually exclusive options.</summary>
    public IReadOnlyCollection<string> Given => _values.Keys;

    public IReadOnlyList<string> Positionals => _positionals;

    public static CliOptions Parse(string[] args, string[]? valueOptions = null, string[]? boolOptions = null)
    {
        if (args.Length == 0)
        {
            return new CliOptions(string.Empty);
        }

        var takesValue = new HashSet<string>(valueOptions ?? NoValueOptions, StringComparer.OrdinalIgnoreCase);
        var takesBool = new HashSet<string>(boolOptions ?? NoValueOptions, StringComparer.OrdinalIgnoreCase);
        var options = new CliOptions(args[0].Trim().ToLowerInvariant());

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];

            if (token.Length == 0)
            {
                continue;
            }

            if (token[0] != '-' || token.Length == 1)
            {
                options._positionals.Add(token);
                continue;
            }

            var name = token.TrimStart('-');
            var hasInlineValue = false;
            var inlineValue = string.Empty;

            var separator = name.IndexOf('=');
            if (separator >= 0)
            {
                inlineValue = name[(separator + 1)..];
                name = name[..separator];
                hasInlineValue = true;
            }

            if (name.Length == 0)
            {
                throw new CliUsageException($"'{token}' is not a valid option.");
            }

            if (takesBool.Contains(name))
            {
                options._values[name] = hasInlineValue ? inlineValue : "true";
                continue;
            }

            if (!takesValue.Contains(name))
            {
                throw new CliUsageException($"Unknown option '--{name}'. Run 'vibe help' to see what is supported.");
            }

            if (hasInlineValue)
            {
                options._values[name] = inlineValue;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new CliUsageException($"Option '--{name}' needs a value.");
            }

            options._values[name] = args[++i];
        }

        return options;
    }

    public bool Has(string name) => _values.ContainsKey(name);

    public string? Value(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public string ValueOr(string name, string fallback) => Value(name) ?? fallback;

    public int? IntValue(string name, out string? error)
    {
        error = null;
        var raw = Value(name);
        if (raw is null)
        {
            return null;
        }

        if (!int.TryParse(raw, out var parsed))
        {
            error = $"Option '--{name}' expects a whole number but got '{raw}'.";
            return null;
        }

        return parsed;
    }

    public T EnumValue<T>(string name, T fallback, out string? error)
        where T : struct, Enum
    {
        error = null;
        var raw = Value(name);
        if (raw is null)
        {
            return fallback;
        }

        foreach (var candidate in Enum.GetValues<T>())
        {
            if (candidate.ToString().Equals(raw, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        error = $"Option '--{name}' expects one of: {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}.";
        return fallback;
    }

    public string RequirePositional(int index, string description)
    {
        if (_positionals.Count <= index || string.IsNullOrWhiteSpace(_positionals[index]))
        {
            throw new CliUsageException($"Missing {description}. Run 'vibe help' for usage.");
        }

        return _positionals[index];
    }

    public void EnsureNoExtraPositionals(int allowed)
    {
        if (_positionals.Count > allowed)
        {
            var extra = string.Join(", ", _positionals.Skip(allowed));
            throw new CliUsageException($"Unexpected argument(s): {extra}");
        }
    }
}
