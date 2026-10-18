using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VibeUninstaller.Windows.Shell;

namespace VibeUninstaller.App.Services;

/// <summary>
/// Loads application icons from registry DisplayIcon values and keeps them
/// cached; a fresh extraction costs a few milliseconds per icon.
/// </summary>
internal static class IconCache
{
    private const int IconSize = 32;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? Get(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        return Cache.GetOrAdd(displayIcon, Load);
    }

    private static ImageSource? Load(string displayIcon)
    {
        var handle = ShellIconLoader.Extract(displayIcon, IconSize);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(IconSize, IconSize));
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            ShellIconLoader.DestroyIcon(handle);
        }
    }
}
