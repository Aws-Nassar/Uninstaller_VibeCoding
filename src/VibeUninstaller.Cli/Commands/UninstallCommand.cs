using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;
using VibeUninstaller.Windows.Execution;

namespace VibeUninstaller.Cli.Commands;

/// <summary>
/// vibe uninstall - runs the application's own uninstaller, scans for what was
/// left behind and (with --clean) removes the leftovers it can prove belong to
/// the application. Nothing is removed until the plan has been printed.
/// </summary>
internal static class UninstallCommand
{
    private static readonly string[] ValueOptions = { "timeout", "min-tier" };
    private static readonly string[] BoolOptions =
        { "json", "silent", "no-scan", "clean", "yes", "dry-run", "include-system" };

    public static async Task<int> RunAsync(string[] args, ConsoleUi ui, CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args, ValueOptions, BoolOptions);
        var query = options.RequirePositional(0, "application name");
        options.EnsureNoExtraPositionals(1);

        var json = options.Has("json");
        var timeoutMinutes = options.IntValue("timeout", out var timeoutError) ?? 30;
        if (timeoutError is not null)
        {
            throw new CliUsageException(timeoutError);
        }

        var dryRun = options.Has("dry-run");
        var clean = options.Has("clean") && !dryRun;
        var minTier = ConsoleUi.ParseTier(options.ValueOr("min-tier", "safe"));
        var matchOptions = AppMatchOptions.FromFlags(options.Has("include-system"), true, true);

        var enumeration = await AppResolver.LoadAsync(cancellationToken).ConfigureAwait(false);
        var app = AppResolver.Resolve(enumeration.Apps, query, matchOptions, ui, json, out var exitCode);
        if (app is null)
        {
            return exitCode;
        }

        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        var installer = InstallerInfoDetector.Detect(app, parsed);
        var command = InstallerInfoDetector.BuildCommand(app, installer, options.Has("silent"));

        if (json)
        {
            return await RunJsonAsync(app, options, command, installer, clean, dryRun, minTier, timeoutMinutes, cancellationToken)
                .ConfigureAwait(false);
        }

        ui.Header($"Uninstalling {app.DisplayName}");
        ui.Line($"  Installer:      {installer.FamilyLabel}");
        ui.Line($"  Command:        {command}");

        if (installer.Note is not null)
        {
            ui.Muted($"  Note:           {installer.Note}");
        }

        if (dryRun)
        {
            var preview = await Task.Run(() => new StaticScanner().Scan(app, json ? null : new Progress<string>(ui.Progress), cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            var previewItems = preview.Items.ToArray();
            ui.Warn("Dry run: nothing was executed and nothing was removed.");
            ui.Leftovers(previewItems);
            ui.Line();
            ui.Muted($"  {previewItems.Length} item(s) would be removable ({SizeFormat.Bytes(previewItems.Sum(i => i.SizeBytes ?? 0))}).");
            ui.Muted($"  --clean would remove the {previewItems.Count(i => i.Tier <= minTier)} item(s) at tier '{ConsoleUi.TierName(minTier)}' or better.");
            return ExitCode.Ok;
        }

        AppResolver.WarnIfNotElevated(ui, json: false);

        var progress = new Progress<UninstallProgress>(update =>
        {
            var detail = string.IsNullOrWhiteSpace(update.Detail) ? string.Empty : $"  {update.Detail}";
            ui.Progress($"  [{update.Stage}] {detail}".TrimEnd());
        });

        var service = new UninstallService();
        var session = await service
            .UninstallAsync(app, new UninstallOptions
            {
                ScanLeftovers = !options.Has("no-scan"),
                UseSilentUninstall = options.Has("silent"),
                Timeout = TimeSpan.FromMinutes(Math.Max(1, timeoutMinutes)),
            }, progress, cancellationToken)
            .ConfigureAwait(false);

        ReportRun(ui, session);

        if (session.Run.Status is UninstallRunStatus.StartFailed)
        {
            return ExitCode.Error;
        }

        if (session.Run.Status is UninstallRunStatus.Failed or UninstallRunStatus.TimedOut)
        {
            return ExitCode.UninstallFailed;
        }

        if (session.Scan is not { } scan)
        {
            return ExitCode.Ok;
        }

        var candidates = scan.Items.Where(item => item.Tier <= minTier).ToArray();
        if (candidates.Length == 0)
        {
            ui.Success("No leftovers were found.");
            return ExitCode.Ok;
        }

        ui.Header($"Leftovers found after the uninstall ({scan.Items.Count} total)");
        ui.Leftovers(candidates);

        if (!clean)
        {
            ui.Line();
            ui.Muted($"  Nothing was removed. Re-run with --clean to remove the {candidates.Length} item(s) above");
            ui.Muted($"  or use --clean --min-tier moderate to include less certain items.");
            return ExitCode.Ok;
        }

        var selected = await ConfirmSelectionAsync(ui, candidates, options.Has("yes"), cancellationToken).ConfigureAwait(false);
        if (selected is null)
        {
            return ExitCode.Ok;
        }

        var removal = await new RemovalExecutor()
            .RemoveAsync(app, selected, new Progress<string>(ui.Muted), cancellationToken)
            .ConfigureAwait(false);

        ui.Header("Removal result");
        ui.Line($"  Files recycled:   {removal.FilesRemoved:N0} ({SizeFormat.Bytes(removal.BytesRemoved)})");
        ui.Line($"  Files failed:     {removal.FilesFailed:N0}");
        ui.Line($"  Registry keys:    {removal.RegistryRemoved:N0} removed, {removal.RegistryFailed:N0} failed");
        if (removal.VaultPath is not null)
        {
            ui.Muted($"  Registry backups: {removal.VaultPath}");
        }

        foreach (var error in removal.Errors.Take(10))
        {
            ui.Warn($"  {error}");
        }

        var failed = removal.FilesFailed + removal.RegistryFailed + removal.Errors.Count;
        return failed == 0 ? ExitCode.Ok : ExitCode.PartialFailure;
    }

    private static async Task<int> RunJsonAsync(
        InstalledApp app,
        CliOptions options,
        InstallCommand command,
        InstallerInfo installer,
        bool clean,
        bool dryRun,
        RiskTier minTier,
        int timeoutMinutes,
        CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            var preview = await Task.Run(() => new StaticScanner().Scan(app, null, cancellationToken), cancellationToken).ConfigureAwait(false);
            var previewItems = preview.Items.ToArray();

            var result = new OperationReport
            {
                App = AppReport.From(app),
                Scan = ScanReport.From("static", preview.ToScanResult(), previewItems.Sum(i => i.SizeBytes ?? 0)) with
                {
                    Total = previewItems.Length,
                    Safe = previewItems.Count(i => i.Tier == RiskTier.Safe),
                    Moderate = previewItems.Count(i => i.Tier == RiskTier.Moderate),
                    Advanced = previewItems.Count(i => i.Tier == RiskTier.Advanced),
                    Items = previewItems.Select(LeftoverReport.From).ToArray(),
                },
                DryRun = true,
                Outcome = "dry-run",
                Messages = new[]
                {
                    $"Would run: {command}",
                    $"Installer family: {installer.FamilyLabel}",
                    $"Clean threshold: {ConsoleUi.TierName(minTier)}",
                },
            };

            Console.Out.WriteLine(ReportJson.Serialize(result));
            return ExitCode.Ok;
        }

        var messages = new List<string>();
        var service = new UninstallService();
        var session = await service
            .UninstallAsync(app, new UninstallOptions
            {
                ScanLeftovers = !options.Has("no-scan"),
                UseSilentUninstall = options.Has("silent"),
                Timeout = TimeSpan.FromMinutes(Math.Max(1, timeoutMinutes)),
            }, null, cancellationToken)
            .ConfigureAwait(false);

        if (session.Run.ErrorMessage is not null)
        {
            messages.Add(session.Run.ErrorMessage);
        }

        var selected = Array.Empty<LeftoverItem>();
        if (clean && session.Scan is { } scan)
        {
            selected = scan.Items.Where(item => item.Tier <= minTier).ToArray();

            if (selected.Length > 0 && !options.Has("yes"))
            {
                messages.Add($"Refusing to remove {selected.Length} item(s) without --yes (non-interactive mode).");
                selected = Array.Empty<LeftoverItem>();
            }
        }

        RemovalReport? removal = null;
        if (selected.Length > 0)
        {
            removal = await new RemovalExecutor()
                .RemoveAsync(app, selected, null, cancellationToken)
                .ConfigureAwait(false);
        }

        var exit = session.Run.Status switch
        {
            UninstallRunStatus.StartFailed => ExitCode.Error,
            UninstallRunStatus.Failed or UninstallRunStatus.TimedOut => ExitCode.UninstallFailed,
            UninstallRunStatus.Canceled => ExitCode.Error,
            _ => ExitCode.Ok,
        };

        if (removal is not null && removal.FilesFailed + removal.RegistryFailed + removal.Errors.Count > 0 && exit == ExitCode.Ok)
        {
            exit = ExitCode.PartialFailure;
        }

        var outcome = exit switch
        {
            ExitCode.UninstallFailed => "uninstall-failed",
            ExitCode.Error => "error",
            ExitCode.PartialFailure => "partial",
            _ when session.Scan is { Items.Count: 0 } => "clean",
            _ when session.Scan is not null => "leftovers-found",
            _ => "uninstalled",
        };

        var report = new OperationReport
        {
            App = AppReport.From(app),
            Run = RunReport.From(session.Run),
            Scan = session.Scan is null ? null : ScanReport.From("post-uninstall", session.Scan),
            Removal = removal is null
                ? null
                : new RemovalReportJson
                {
                    FilesRemoved = removal.FilesRemoved,
                    FilesFailed = removal.FilesFailed,
                    RegistryRemoved = removal.RegistryRemoved,
                    RegistryFailed = removal.RegistryFailed,
                    BytesRemoved = removal.BytesRemoved,
                    VaultPath = removal.VaultPath,
                    Errors = removal.Errors,
                },
            DryRun = false,
            Outcome = outcome,
            Messages = messages,
        };

        Console.Out.WriteLine(ReportJson.Serialize(report));
        return exit;
    }

    private static void ReportRun(ConsoleUi ui, UninstallSessionResult session)
    {
        var run = session.Run;
        var message = run.Status switch
        {
            UninstallRunStatus.Succeeded => $"The uninstaller finished successfully{(run.RebootRequired ? " and asked for a restart" : string.Empty)}.",
            UninstallRunStatus.Failed => "The uninstaller reported a failure.",
            UninstallRunStatus.TimedOut => "The uninstaller did not finish in time.",
            UninstallRunStatus.Canceled => "The uninstall was canceled.",
            _ => "The uninstaller could not be started.",
        };

        if (run.Status == UninstallRunStatus.Succeeded)
        {
            ui.Success(message);
        }
        else
        {
            ui.Warn(message);
        }

        ui.Muted($"  Exit code: {run.ExitCode?.ToString() ?? "unknown"} · {run.Duration.TotalSeconds:N1}s · {run.ProcessCount} process(es)");

        if (run.ErrorMessage is not null)
        {
            ui.Muted($"  {run.ErrorMessage}");
        }
    }

    private static async Task<IReadOnlyList<LeftoverItem>?> ConfirmSelectionAsync(
        ConsoleUi ui,
        IReadOnlyList<LeftoverItem> candidates,
        bool assumeYes,
        CancellationToken cancellationToken)
    {
        if (assumeYes)
        {
            return candidates;
        }

        var total = SizeFormat.Bytes(candidates.Sum(item => item.SizeBytes ?? 0));
        if (!ui.Confirm($"About to remove {candidates.Count} item(s) ({total})."))
        {
            ui.Muted("Nothing was removed.");
            return null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return candidates;
    }
}
