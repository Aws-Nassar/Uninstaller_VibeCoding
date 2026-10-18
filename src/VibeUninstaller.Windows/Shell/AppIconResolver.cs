using System.IO;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Shell;

/// <summary>What an icon lookup produced.</summary>
public enum AppIconKind
{
    /// <summary>Nothing usable was found; <see cref="AppIconResult.Reason"/> says why.</summary>
    None,

    /// <summary>An icon handle from an executable, .ico or .dll; the caller must destroy it.</summary>
    IconHandle,

    /// <summary>An image file (a Store/MSIX logo) the caller can load directly.</summary>
    ImageFile,
}

/// <summary>The outcome of one icon lookup.</summary>
public sealed record AppIconResult(AppIconKind Kind, nint Handle, string? ImagePath, string Reason);

/// <summary>
/// Finds the best available icon for an installed application: what the
/// installer registered, otherwise the application's own executable, otherwise
/// the logo of a Store package. Every failure carries a reason for the UI.
/// </summary>
public static class AppIconResolver
{
    private const int MaxExecutablesScanned = 400;
    private const int MaxScanDepth = 2;

    public static AppIconResult Resolve(InstalledApp app, int size = 32)
    {
        var sawInstallFolder = false;

        foreach (var candidate in IconResolver.Candidates(app))
        {
            if (candidate.Kind == IconSourceKind.IconReference)
            {
                if (Expand(candidate.Path) is not { } path || !File.Exists(path))
                {
                    continue;
                }

                var handle = ShellIconLoader.ExtractFromFile(path, candidate.Index, size);
                if (handle != 0)
                {
                    return new AppIconResult(AppIconKind.IconHandle, handle, null, "Icon registered by the installer");
                }

                continue;
            }

            if (Expand(candidate.Path) is not { } folder || !Directory.Exists(folder))
            {
                continue;
            }

            sawInstallFolder = true;
            var executable = PickExecutable(app, folder);
            if (executable is null)
            {
                continue;
            }

            var icon = ShellIconLoader.ExtractFromFile(executable, 0, size);
            if (icon != 0)
            {
                return new AppIconResult(
                    AppIconKind.IconHandle,
                    icon,
                    null,
                    $"Icon of {Path.GetFileName(executable)} in the install folder");
            }
        }

        if (AppxLogoLocator.Find(app.InstallLocation) is { } logo)
        {
            return new AppIconResult(AppIconKind.ImageFile, 0, logo, "Logo of the Store package");
        }

        return new AppIconResult(AppIconKind.None, 0, null, DescribeFailure(app, sawInstallFolder));
    }

    private static string DescribeFailure(InstalledApp app, bool sawInstallFolder)
    {
        if (app.Source == AppSource.Appx)
        {
            return "The Store package does not expose a logo image";
        }

        if (string.IsNullOrWhiteSpace(app.DisplayIcon))
        {
            return sawInstallFolder
                ? "No icon was registered and the install folder holds nothing usable"
                : "The installer did not register an icon";
        }

        return sawInstallFolder
            ? "The registered icon could not be read and the install folder holds nothing usable"
            : "The registered icon file could not be read";
    }

    private static string? Expand(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return expanded.Length == 0 ? null : expanded;
    }

    private static string? PickExecutable(InstalledApp app, string folder)
    {
        var files = new List<ExecutableFile>(64);
        Collect(folder, 0, files);
        return files.Count == 0 ? null : IconResolver.PickExecutable(app, files);
    }

    private static void Collect(string folder, int depth, List<ExecutableFile> files)
    {
        if (files.Count >= MaxExecutablesScanned)
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.exe"))
            {
                if (files.Count >= MaxExecutablesScanned)
                {
                    return;
                }

                long size = 0;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch (IOException)
                {
                    // An unreadable file simply scores without its size bonus.
                }

                files.Add(new ExecutableFile(path, size, depth));
            }

            if (depth >= MaxScanDepth)
            {
                return;
            }

            foreach (var child in Directory.EnumerateDirectories(folder))
            {
                Collect(child, depth + 1, files);
            }
        }
        catch (IOException)
        {
            // A missing or locked folder is not an error here.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
