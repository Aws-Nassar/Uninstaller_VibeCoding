using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;
using VibeUninstaller.Windows.Providers;

namespace VibeUninstaller.Cli.Commands;

/// <summary>Shared helpers for resolving an application from a user supplied query.</summary>
internal static class AppResolver
{
    public static async Task<AppEnumerationResult> LoadAsync(CancellationToken cancellationToken) =>
        await new AppEnumerator().GetInstalledAppsAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Resolves a query to exactly one application. Writes a helpful message and
    /// returns null when nothing matched or the query is ambiguous.
    /// </summary>
    public static InstalledApp? Resolve(
        IReadOnlyList<InstalledApp> apps,
        string query,
        AppMatchOptions options,
        ConsoleUi ui,
        bool json,
        out int exitCode)
    {
        exitCode = ExitCode.Ok;
        var match = AppMatcher.Match(apps, query, options);

        switch (match.Kind)
        {
            case AppMatchKind.Single:
                return match.App;

            case AppMatchKind.Ambiguous:
                exitCode = ExitCode.Ambiguous;
                ui.Error($"{match.Candidates.Count} applications match '{query}'. Use a more specific name or the id:");

                foreach (var candidate in match.Candidates.Take(15))
                {
                    ui.Muted($"  {candidate.DisplayName}  [{candidate.Id}]");
                }

                return null;

            default:
                exitCode = ExitCode.NotFound;
                ui.Error($"No installed application matches '{query}'.");

                if (match.Candidates.Count > 0)
                {
                    ui.Muted("Did you mean one of these?");
                    foreach (var candidate in match.Candidates)
                    {
                        ui.Muted($"  {candidate.DisplayName}");
                    }
                }

                return null;
        }
    }

    /// <summary>Print a hint when the current process is not elevated.</summary>
    public static void WarnIfNotElevated(ConsoleUi ui, bool json)
    {
        if (!json && !VibeUninstaller.Windows.Execution.AdminCheck.IsElevated)
        {
            ui.Warn("Note: this shell is not elevated. Elevation is requested per action when needed.");
        }
    }
}
