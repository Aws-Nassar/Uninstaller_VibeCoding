using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;

namespace VibeUninstaller.Cli.Commands;

/// <summary>vibe list - show everything that is installed.</summary>
internal static class ListCommand
{
    private static readonly string[] ValueOptions = { "search", "source", "scope", "sort", "limit", "min-size" };
    private static readonly string[] BoolOptions = { "json", "include-system", "no-store", "no-steam" };

    public static async Task<int> RunAsync(string[] args, ConsoleUi ui, CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args, ValueOptions, BoolOptions);
        options.EnsureNoExtraPositionals(0);

        var limit = options.IntValue("limit", out var limitError);
        if (limitError is not null)
        {
            throw new CliUsageException(limitError);
        }

        var minSizeMb = options.IntValue("min-size", out var sizeError);
        if (sizeError is not null)
        {
            throw new CliUsageException(sizeError);
        }

        var json = options.Has("json");
        var matchOptions = AppMatchOptions.FromFlags(
            options.Has("include-system"),
            includeStore: !options.Has("no-store"),
            includeSteam: !options.Has("no-steam"));

        var enumeration = await AppResolver.LoadAsync(cancellationToken).ConfigureAwait(false);
        var apps = AppMatcher.Filter(enumeration.Apps, matchOptions);

        var search = options.Value("search");
        if (!string.IsNullOrWhiteSpace(search))
        {
            apps = apps
                .Where(app => app.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (app.Publisher?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToArray();
        }

        var source = options.Value("source");
        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            apps = apps.Where(app => MatchesSource(app, source)).ToArray();
        }

        var scope = options.Value("scope");
        if (!string.IsNullOrWhiteSpace(scope) && !scope.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var wantMachine = scope.StartsWith("m", StringComparison.OrdinalIgnoreCase);
            apps = apps.Where(app => (app.Scope == InstallScope.Machine) == wantMachine).ToArray();
        }

        if (minSizeMb is > 0)
        {
            var threshold = minSizeMb.Value * 1024L * 1024L;
            apps = apps.Where(app => (app.EstimatedSizeBytes ?? 0) >= threshold).ToArray();
        }

        apps = Sort(apps, options.ValueOr("sort", "name"));

        if (limit is > 0)
        {
            apps = apps.Take(limit.Value).ToArray();
        }

        if (json)
        {
            var report = new ListReport
            {
                Count = apps.Count,
                Apps = apps.Select(AppReport.From).ToArray(),
                Warnings = enumeration.Warnings,
            };

            Console.Out.WriteLine(ReportJson.Serialize(report));
            return ExitCode.Ok;
        }

        if (apps.Count == 0)
        {
            ui.Warn("No applications matched.");
            return ExitCode.Ok;
        }

        var rows = apps
            .Select(app => new[]
            {
                app.DisplayName,
                app.Version ?? "-",
                app.Publisher ?? "-",
                SizeFormat.Bytes(app.EstimatedSizeBytes),
                app.SourceLabel,
                app.ScopeLabel,
            })
            .ToArray();

        ui.Table(
            new[] { "NAME", "VERSION", "PUBLISHER", "SIZE", "SOURCE", "SCOPE" },
            rows,
            new[] { 46, 16, 28, 10, 8, 12 });

        ui.Line();
        ui.Muted($"{apps.Count} application(s). Use 'vibe info <name>' for details and 'vibe scan <name>' to look for leftovers.");

        foreach (var warning in enumeration.Warnings)
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        return ExitCode.Ok;
    }

    private static bool MatchesSource(InstalledApp app, string source) =>
        app.Source switch
        {
            AppSource.Appx => source.StartsWith("store", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("appx", StringComparison.OrdinalIgnoreCase),
            AppSource.Steam => source.StartsWith("steam", StringComparison.OrdinalIgnoreCase),
            _ => source.StartsWith("registry", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("win32", StringComparison.OrdinalIgnoreCase),
        };

    private static IReadOnlyList<InstalledApp> Sort(IReadOnlyList<InstalledApp> apps, string sort) => sort.ToLowerInvariant() switch
    {
        "size" => apps.OrderByDescending(app => app.EstimatedSizeBytes ?? 0).ToArray(),
        "date" => apps.OrderByDescending(app => app.InstallDate ?? DateOnly.MinValue).ToArray(),
        "publisher" => apps
            .OrderBy(app => app.Publisher ?? "zzz", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray(),
        _ => apps.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray(),
    };
}
