using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Providers;

/// <summary>A source of installed applications.</summary>
public interface IAppProvider
{
    string Name { get; }

    Task<IReadOnlyList<InstalledApp>> GetAppsAsync(CancellationToken cancellationToken);
}
