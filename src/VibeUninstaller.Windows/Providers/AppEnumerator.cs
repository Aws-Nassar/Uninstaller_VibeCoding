using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Providers;

public sealed record AppEnumerationResult(IReadOnlyList<InstalledApp> Apps, IReadOnlyList<string> Warnings);

/// <summary>Runs every provider, hides duplicates and produces one list.</summary>
public sealed class AppEnumerator
{
    private readonly IReadOnlyList<IAppProvider> _providers = new IAppProvider[]
    {
        new RegistryAppProvider(),
        new AppxAppProvider(),
        new SteamAppProvider(),
    };

    public async Task<AppEnumerationResult> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var apps = new List<InstalledApp>();

        var tasks = _providers.Select(async provider =>
        {
            try
            {
                var found = await provider.GetAppsAsync(cancellationToken).ConfigureAwait(false);
                return (provider.Name, Apps: found, Error: (string?)null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return (provider.Name, Apps: (IReadOnlyList<InstalledApp>)Array.Empty<InstalledApp>(), Error: exception.Message);
            }
        });

        foreach (var result in await Task.WhenAll(tasks).ConfigureAwait(false))
        {
            apps.AddRange(result.Apps);
            if (result.Error is not null)
            {
                warnings.Add($"{result.Name}: {result.Error}");
            }
        }

        var deduplicated = Deduplicate(apps)
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return new AppEnumerationResult(deduplicated, warnings);
    }

    private static IEnumerable<InstalledApp> Deduplicate(List<InstalledApp> apps)
    {
        var byKey = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in apps)
        {
            var key = $"{app.Source}|{app.DisplayName.Trim()}|{app.Version ?? string.Empty}|{app.Scope}";
            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = app;
                continue;
            }

            byKey[key] = Prefer(existing, app);
        }

        return byKey.Values;
    }

    private static InstalledApp Prefer(InstalledApp left, InstalledApp right)
    {
        var leftScore = Score(left);
        var rightScore = Score(right);
        return rightScore > leftScore ? right : left;

        static int Score(InstalledApp app)
        {
            var score = 0;
            if (!string.IsNullOrWhiteSpace(app.UninstallString))
            {
                score += 2;
            }

            if (!string.IsNullOrWhiteSpace(app.QuietUninstallString))
            {
                score += 1;
            }

            if (!string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                score += 1;
            }

            if (app.Scope == InstallScope.Machine)
            {
                score += 1;
            }

            return score;
        }
    }
}
