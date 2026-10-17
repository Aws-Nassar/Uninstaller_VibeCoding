using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;

namespace VibeUninstaller.Cli;

/// <summary>Console output helpers: colors, tables and leftover listings.</summary>
public sealed class ConsoleUi
{
    private readonly bool _color;

    public ConsoleUi(bool color)
    {
        _color = color;
    }

    public bool IsJsonQuiet { get; init; }

    public static ConsoleUi Create(bool noColor, bool json)
    {
        var color = !noColor && !json && !Console.IsOutputRedirected;
        return new ConsoleUi(color) { IsJsonQuiet = json };
    }

    public void Write(string text) => Console.Out.Write(text);

    public void Line(string text = "") => Console.Out.WriteLine(text);

    public void Info(string text) => WriteColor(text, null);

    public void Muted(string text) => WriteColor(text, ConsoleColor.DarkGray);

    /// <summary>Progress and diagnostics: always stderr so stdout stays parseable.</summary>
    public void Progress(string text)
    {
        if (!IsJsonQuiet)
        {
            Console.Error.WriteLine(text);
        }
    }

    public void Success(string text) => WriteColor(text, ConsoleColor.Green);

    public void Warn(string text) => WriteColor(text, ConsoleColor.Yellow);

    public void Error(string text) => WriteColor(text, ConsoleColor.Red);

    public void Header(string text)
    {
        Line();
        WriteColor(text, ConsoleColor.Cyan);
    }

    private void WriteColor(string text, ConsoleColor? color)
    {
        if (IsJsonQuiet)
        {
            return;
        }

        if (!_color || color is null)
        {
            Console.Out.WriteLine(text);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color.Value;
            Console.Out.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    /// <summary>Prints a plain text table with two spaces between columns.</summary>
    public void Table(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, int[]? maxWidths = null)
    {
        if (IsJsonQuiet)
        {
            return;
        }

        var columns = headers.Count;
        var widths = new int[columns];
        for (var i = 0; i < columns; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < columns && i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        if (maxWidths is not null)
        {
            for (var i = 0; i < columns && i < maxWidths.Length; i++)
            {
                if (maxWidths[i] > 0)
                {
                    widths[i] = Math.Min(widths[i], maxWidths[i]);
                }
            }
        }

        Line(JoinRow(headers, widths, false));
        Muted(new string('-', Math.Min(widths.Sum() + (columns - 1) * 2, 100)));

        foreach (var row in rows)
        {
            Line(JoinRow(row, widths, true));
        }

        static string JoinRow(IReadOnlyList<string> values, int[] widths, bool truncate)
        {
            var parts = new string[widths.Length];
            for (var i = 0; i < widths.Length; i++)
            {
                var value = i < values.Count ? values[i] : string.Empty;
                if (truncate && value.Length > widths[i])
                {
                    value = widths[i] > 3 ? value[..(widths[i] - 3)] + "..." : value[..widths[i]];
                }

                parts[i] = value.PadRight(widths[i]);
            }

            return string.Join("  ", parts).TrimEnd();
        }
    }

    /// <summary>Prints leftovers grouped by risk tier with the reason for each item.</summary>
    public void Leftovers(IReadOnlyList<LeftoverItem> items)
    {
        if (IsJsonQuiet || items.Count == 0)
        {
            return;
        }

        foreach (var group in items.GroupBy(item => item.Tier).OrderBy(group => group.Key))
        {
            Line();
            WriteColor($"{TierName(group.Key).ToUpperInvariant()} ({group.Count()})", TierColor(group.Key));

            foreach (var item in group)
            {
                var size = item.IsRegistry ? "registry" : SizeFormat.Bytes(item.SizeBytes);
                Line($"  [{item.KindLabel.ToLowerInvariant()}] {size,-10} {item.Path}");
                Muted($"      -> {item.Reason}");
            }
        }
    }

    public static string TierName(RiskTier tier) => tier switch
    {
        RiskTier.Safe => "safe",
        RiskTier.Moderate => "moderate",
        _ => "advanced",
    };

    public static RiskTier ParseTier(string? value) => value?.ToLowerInvariant() switch
    {
        "safe" => RiskTier.Safe,
        "moderate" or "medium" => RiskTier.Moderate,
        _ => RiskTier.Advanced,
    };

    private static ConsoleColor TierColor(RiskTier tier) => tier switch
    {
        RiskTier.Safe => ConsoleColor.Green,
        RiskTier.Moderate => ConsoleColor.Yellow,
        _ => ConsoleColor.DarkYellow,
    };

    /// <summary>Asks a yes/no question; returns false when input is not interactive.</summary>
    public bool Confirm(string question)
    {
        if (Console.IsInputRedirected)
        {
            Error("Input is not interactive; pass --yes to confirm this action.");
            return false;
        }

        Line();
        Line($"{question} Type YES to continue: ");
        var answer = Console.ReadLine();
        return string.Equals(answer?.Trim(), "YES", StringComparison.OrdinalIgnoreCase);
    }
}
