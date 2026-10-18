using System.Xml;
using System.Xml.Linq;

namespace VibeUninstaller.Core.Analysis;

/// <summary>
/// Reads the logo image path out of an MSIX/Store package manifest
/// (AppxManifest.xml). Pure XML handling, so the rules can be tested without a
/// packaged application.
/// </summary>
public static class AppxLogoParser
{
    /// <summary>
    /// Returns the logo path the package declares, or null when the manifest
    /// names none. Values like "ms-resource:" are not file paths and are
    /// reported as missing.
    /// </summary>
    public static string? ParseLogoPath(string? manifestXml)
    {
        if (string.IsNullOrWhiteSpace(manifestXml))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(manifestXml, LoadOptions.None);

            // The application tile logo is the most representative image and is
            // named after its "44x44" target size.
            var tileLogo = document
                .Descendants()
                .Attributes()
                .FirstOrDefault(attribute =>
                    attribute.Name.LocalName.Equals("Square44x44Logo", StringComparison.OrdinalIgnoreCase));

            if (IsUsablePath(tileLogo?.Value))
            {
                return Clean(tileLogo!.Value);
            }

            // Older packages only carry the package logo.
            var packageLogo = document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("Logo", StringComparison.OrdinalIgnoreCase));

            return IsUsablePath(packageLogo?.Value) ? Clean(packageLogo!.Value) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string Clean(string value) => value.Trim().Replace('/', '\\');

    private static bool IsUsablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return trimmed.IndexOfAny(Path.GetInvalidPathChars()) < 0;
    }
}
