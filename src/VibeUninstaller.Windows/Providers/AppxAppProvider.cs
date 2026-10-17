using Windows.ApplicationModel;
using Windows.Management.Deployment;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Providers;

/// <summary>Discovers Microsoft Store / MSIX packages for the current user.</summary>
public sealed class AppxAppProvider : IAppProvider
{
    private static readonly string[] FrameworkNamePrefixes =
    {
        "Microsoft.NET.",
        "Microsoft.VCLibs.",
        "Microsoft.UI.Xaml",
        "Microsoft.WindowsAppRuntime",
        "Microsoft.Services.Store.Engagement",
    };

    public string Name => "Store";

    public Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<InstalledApp>>(() => Read(cancellationToken), cancellationToken);

    private static List<InstalledApp> Read(CancellationToken cancellationToken)
    {
        var apps = new List<InstalledApp>(256);
        var manager = new PackageManager();
        var packages = manager.FindPackagesForUser(string.Empty);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (package.IsFramework || package.IsResourcePackage || package.IsBundle)
            {
                continue;
            }

            var id = package.Id;
            if (id is null)
            {
                continue;
            }

            if (FrameworkNamePrefixes.Any(prefix => id.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!seenNames.Add(id.Name))
            {
                continue;
            }

            var displayName = ResolveDisplayName(package);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            var isSystem = package.SignatureKind == PackageSignatureKind.System;

            apps.Add(new InstalledApp
            {
                Id = $"appx|{id.FullName}",
                DisplayName = displayName,
                Version = $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}",
                Publisher = SafePublisher(package),
                InstallLocation = SafeInstalledPath(package),
                InstallDate = ReadInstallDate(package),
                PackageFullName = id.FullName,
                Source = AppSource.Appx,
                Scope = InstallScope.User,
                IsSystemComponent = isSystem,
            });
        }

        return apps;
    }

    /// <summary>Removes a package through the deployment API.</summary>
    public static async Task<(bool Success, string? Error)> RemoveAsync(
        string packageFullName,
        CancellationToken cancellationToken)
    {
        try
        {
            var manager = new PackageManager();
            var result = await manager
                .RemovePackageAsync(packageFullName, RemovalOptions.None)
                .AsTask(cancellationToken);

            if (result.ExtendedErrorCode is null)
            {
                return (true, null);
            }

            return (false, result.ErrorText);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }

    private static string? ResolveDisplayName(Package package)
    {
        try
        {
            var name = package.DisplayName;
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }
        catch (Exception)
        {
            // Fall through to the package identity name.
        }

        var identityName = package.Id?.Name;
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return null;
        }

        var lastDot = identityName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < identityName.Length - 1
            ? identityName[(lastDot + 1)..]
            : identityName;
    }

    private static string? SafePublisher(Package package)
    {
        try
        {
            return package.PublisherDisplayName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? SafeInstalledPath(Package package)
    {
        try
        {
            return package.InstalledPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DateOnly? ReadInstallDate(Package package)
    {
        try
        {
            return DateOnly.FromDateTime(package.InstalledDate.DateTime);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
