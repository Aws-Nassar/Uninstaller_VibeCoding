using System.Text.RegularExpressions;

namespace VibeUninstaller.Core.Execution;

/// <summary>
/// Splits an uninstall command line into executable and arguments without
/// relying on the shell. Handles quoted paths, unquoted paths with spaces,
/// msiexec and rundll32 style commands, and stray quotes.
/// </summary>
public static partial class CommandLineSplitter
{
    public static (string FileName, string Arguments)? Split(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var command = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (command.Length == 0)
        {
            return null;
        }

        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                var fileName = command[1..closingQuote].Trim();
                var arguments = command[(closingQuote + 1)..].Trim();
                if (fileName.Length > 0)
                {
                    return (fileName, arguments);
                }
            }
        }

        var exeIndex = FindExeBoundary(command);
        if (exeIndex > 0)
        {
            var fileName = command[..(exeIndex + 4)].Trim().TrimStart('"').TrimEnd('"');
            var arguments = command[(exeIndex + 4)..].Trim().TrimStart('"').TrimEnd('"').Trim();
            if (fileName.Length > 0)
            {
                return (fileName, arguments);
            }
        }

        var stripped = command.Trim('"').Trim();
        if (!stripped.Contains(' '))
        {
            return (stripped, string.Empty);
        }

        var lastSpace = stripped.LastIndexOf(' ');
        var fallbackName = stripped[..lastSpace].Trim('"').Trim();
        var fallbackArguments = stripped[(lastSpace + 1)..].Trim().Trim('"');
        return fallbackName.Length == 0 ? null : (fallbackName, fallbackArguments);
    }

    /// <summary>Extracts the MSI product code from an msiexec command line.</summary>
    public static string? TryGetMsiProductCode(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var match = MsiProductCodeRegex().Match(commandLine);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Extracts an executable path from a command line, if it is a real path.</summary>
    public static string? TryGetExecutablePath(string? commandLine)
    {
        var parsed = Split(commandLine);
        if (parsed is null)
        {
            return null;
        }

        var fileName = parsed.Value.FileName;

        // Protocol handlers such as "steam://uninstall/440" are not file paths.
        if (fileName.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        return fileName.Contains('\\') || fileName.Contains(':') ? fileName : null;
    }

    private static int FindExeBoundary(string command)
    {
        var searchFrom = 0;
        while (searchFrom < command.Length)
        {
            var index = command.IndexOf(".exe", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return -1;
            }

            var end = index + 4;
            if (end == command.Length || command[end] == ' ' || command[end] == '"')
            {
                return index;
            }

            searchFrom = end;
        }

        return -1;
    }

    [GeneratedRegex(@"/(?:X|I)\s*""?(\{[0-9A-Fa-f\-]{36}\})", RegexOptions.IgnoreCase)]
    private static partial Regex MsiProductCodeRegex();
}
