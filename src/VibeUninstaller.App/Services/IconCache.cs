using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Windows.Shell;

namespace VibeUninstaller.App.Services;

/// <summary>The icon of one application; the reason explains a missing one.</summary>
public readonly record struct AppIcon(ImageSource? Image, string Reason);

/// <summary>
/// Resolves the icon of every application and remembers the result. A lookup
/// touches the disk, so it runs off the UI thread and happens only once per
/// application.
/// </summary>
internal static class IconCache
{
    private const int IconSize = 32;

    private static readonly ConcurrentDictionary<string, AppIcon> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static AppIcon Get(InstalledApp app) => Cache.GetOrAdd(app.Id, _ => Load(app));

    private static AppIcon Load(InstalledApp app)
    {
        AppIconResult result;
        try
        {
            result = AppIconResolver.Resolve(app, IconSize);
        }
        catch (Exception exception)
        {
            return new AppIcon(null, exception.Message);
        }

        return result.Kind switch
        {
            AppIconKind.IconHandle => new AppIcon(FromHandle(result.Handle), result.Reason),
            AppIconKind.ImageFile => new AppIcon(FromFile(result.ImagePath!), result.Reason),
            _ => new AppIcon(null, result.Reason),
        };
    }

    private static ImageSource? FromHandle(nint handle)
    {
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

    private static ImageSource? FromFile(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = IconSize;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            // A logo in a format WPF cannot decode simply falls back to the tile.
            return null;
        }
    }
}
