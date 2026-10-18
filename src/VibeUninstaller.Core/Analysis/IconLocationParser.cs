using System.Globalization;

namespace VibeUninstaller.Core.Analysis;

/// <summary>A parsed DisplayIcon value: a file plus an optional icon index.</summary>
public sealed record IconReference(string Path, int Index);

/// <summary>
/// Parses the registry "DisplayIcon" format, for example
/// "C:\Program Files\App\app.exe,0" or "%ProgramFiles%\App\app.exe,-101".
/// The path may still contain environment variables; expanding and validating
/// it is the caller's job because that part is platform specific.
/// </summary>
public static class IconLocationParser
{
    public static IconReference? Parse(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var value = location.Trim().Trim('"').Trim();
        if (value.Length == 0 || value.All(character => character is ',' or '"' or ' '))
        {
            return null;
        }

        var index = 0;
        var comma = value.LastIndexOf(',');
        if (comma > 0 && comma < value.Length - 1)
        {
            var suffix = value[(comma + 1)..].Trim();
            if (int.TryParse(suffix, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
            {
                // "\"C:\app.exe\",0" style entries keep a quote in front of the comma.
                index = parsed;
                value = value[..comma].Trim().Trim('"').Trim();
            }
        }

        if (value.Length == 0 || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        return new IconReference(value, index);
    }
}
