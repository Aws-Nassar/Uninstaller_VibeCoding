using System.IO;
using System.Runtime.InteropServices;
using VibeUninstaller.Core.Analysis;

namespace VibeUninstaller.Windows.Shell;

/// <summary>
/// Extracts the icon referenced by a registry DisplayIcon value.
/// The returned handle belongs to the caller and must be released with
/// <see cref="DestroyIcon"/>.
/// </summary>
public static class ShellIconLoader
{
    /// <summary>Extracts the icon an installer registered, or 0 when there is none.</summary>
    public static nint Extract(string? displayIcon, int size = 32)
    {
        var reference = IconLocationParser.Parse(displayIcon);
        if (reference is null)
        {
            return 0;
        }

        var path = Environment.ExpandEnvironmentVariables(reference.Path);
        return path.Length == 0 || !File.Exists(path) ? 0 : ExtractFromFile(path, reference.Index, size);
    }

    /// <summary>
    /// Extracts the icon at a known file. Registering a wrong (or negative)
    /// index is common, so the first icon and a smaller size are tried before
    /// giving up.
    /// </summary>
    public static nint ExtractFromFile(string path, int index, int size = 32)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        if (TryExtract(path, index, size, out var handle))
        {
            return handle;
        }

        // Negative indexes address resource ids that SHDefExtractIcon does not
        // understand; falling back to the first icon still shows something useful.
        if (index != 0 && TryExtract(path, 0, size, out handle))
        {
            return handle;
        }

        return size == 16 ? 0 : TryExtract(path, 0, 16, out handle) ? handle : 0;
    }

    private static bool TryExtract(string path, int index, int size, out nint handle)
    {
        handle = 0;
        try
        {
            var packedSize = (uint)(size & 0xFFFF) | ((uint)(size & 0xFFFF) << 16);
            if (SHDefExtractIconW(path, index, 0, out var large, out var small, packedSize) != 0)
            {
                return false;
            }

            if (large != 0)
            {
                if (small != 0)
                {
                    DestroyIcon(small);
                }

                handle = large;
                return true;
            }

            handle = small;
            return small != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHDefExtractIconW(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out nint phiconLarge,
        out nint phiconSmall,
        uint nIconSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(nint hIcon);
}
