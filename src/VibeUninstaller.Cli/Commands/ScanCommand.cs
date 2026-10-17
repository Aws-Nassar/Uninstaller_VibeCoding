using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;
using VibeUninstaller.Windows.Execution;

namespace VibeUninstaller.Cli.Commands;

/// <summary>
/// vibe scan - fingerprint based leftover scan that does not run an uninstaller
/// and changes nothing. This is the dry-run preview of what could be removed.
/// </summary>
internal static class ScanCommand
{
    private static readonly string[] ValueOptions = { "min-tier", "timeout" };
    private static readonly string[] BoolOptions = { "json", "include-system" };

    public static async Task<int> RunAsync(string[] args, ConsoleUi ui, CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args, ValueOptions, BoolOptions);
        var query = options.RequirePositional(0, "application name");
        options.EnsureNoExtraPositionals(1);

        var json = options.Has("json");
        var minTier = ConsoleUi.ParseTier(options.ValueOr("min-tier", "advanced"));
        var matchOptions = AppMatchOptions.FromFlags(options.Has("include-system"), true, true);

        var enumeration = await AppResolver.LoadAsync(cancellationToken).ConfigureAwait(false);
        var app = AppResolver.Resolve(enumeration.Apps, query, matchOptions, ui, json, out var exitCode);
        if (app is null)
        {
            return exitCode;
        }

        if (!app.SupportsLeftoverScan)
        {
            ui.Warn($"{app.DisplayName} is a Store package; its files are managed by Windows. Nothing to scan.");
            return ExitCode.Ok;
        }

        var progress = new Progress<string>(ui.Progress);
        if (!json)
        {
            ui.Header($"Leftover scan for {app.DisplayName}");
        }

        var scanner = new StaticScanner();
        var scan = await Task.Run(
            () => scanner.Scan(app, json ? null : progress, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var items = scan.Items.Where(item => item.Tier <= minTier).ToArray();

        if (json)
        {
            var scanReport = new ScanReport
            {
                Mode = "static",
                Total = items.Length,
                Safe = items.Count(item => item.Tier == RiskTier.Safe),
                Moderate = items.Count(item => item.Tier == RiskTier.Moderate),
                Advanced = items.Count(item => item.Tier == RiskTier.Advanced),
                SizeBytes = items.Sum(item => item.SizeBytes ?? 0),
                DurationMs = (long)scan.Duration.TotalMilliseconds,
                Items = items.Select(LeftoverReport.From).ToArray(),
            };

            var report = new OperationReport
            {
                App = AppReport.From(app),
                Scan = scanReport,
                DryRun = true,
                Outcome = items.Length == 0 ? "clean" : "leftovers-found",
                Messages = scan.Warnings,
            };

            Console.Out.WriteLine(ReportJson.Serialize(report));
            return ExitCode.Ok;
        }

        ui.Muted($"  Analyzed {scan.FileCandidates:N0} file and {scan.RegistryCandidates:N0} registry candidate(s) in {scan.Duration.TotalSeconds:N1}s");
        ui.Line();
        if (items.Length == 0)
        {
            ui.Success("Nothing was left behind. No files or registry keys match this application.");
            return ExitCode.Ok;
        }

        ui.Leftovers(items);

        ui.Line();
        ui.Line(
            $"  {items.Length} item(s), {SizeFormat.Bytes(items.Sum(item => item.SizeBytes ?? 0))} total " +
            $"({items.Count(i => i.Tier == RiskTier.Safe)} safe, " +
            $"{items.Count(i => i.Tier == RiskTier.Moderate)} moderate, " +
            $"{items.Count(i => i.Tier == RiskTier.Advanced)} advanced)");
        ui.Muted("  Nothing was changed. Use 'vibe uninstall <name> --clean' to run the uninstaller and remove them.");

        return ExitCode.Ok;
    }
}
