using System.Globalization;

namespace VibeUninstaller.Core.Reporting;

/// <summary>Human readable byte sizes, shared by the CLI and the GUI.</summary>
public static class SizeFormat
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    /// <summary>Formats a size such as "5.6 MB". Unknown sizes become "-".</summary>
    public static string Bytes(long? bytes)
    {
        if (bytes is null || bytes < 0)
        {
            return "-";
        }

        if (bytes < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes.Value} B");
        }

        double size = bytes.Value;
        var unit = 0;
        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // Rounding can turn "1023.9 KB" into "1,024 KB"; promote the unit instead.
        while (unit < Units.Length - 1 && Math.Round(size, unit == 1 ? 0 : 1) >= 1024)
        {
            size /= 1024;
            unit++;
        }

        var formatted = size.ToString(unit == 1 ? "N0" : "N1", CultureInfo.InvariantCulture);
        return $"{formatted} {Units[unit]}";
    }
}
