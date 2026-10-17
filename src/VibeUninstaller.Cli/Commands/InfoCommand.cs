using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Core.Reporting;

namespace VibeUninstaller.Cli.Commands;

/// <summary>vibe info - everything known about one application, including its fingerprint.</summary>
internal static class InfoCommand
{
    private static readonly string[] ValueOptions = Array.Empty<string>();
    private static readonly string[] BoolOptions = { "json", "include-system" };

    public static async Task<int> RunAsync(string[] args, ConsoleUi ui, CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args, ValueOptions, BoolOptions);
        var query = options.RequirePositional(0, "application name");
        options.EnsureNoExtraPositionals(1);

        var json = options.Has("json");
        var matchOptions = AppMatchOptions.FromFlags(options.Has("include-system"), true, true);

        var enumeration = await AppResolver.LoadAsync(cancellationToken).ConfigureAwait(false);
        var app = AppResolver.Resolve(enumeration.Apps, query, matchOptions, ui, json, out var exitCode);
        if (app is null)
        {
            return exitCode;
        }

        var environment = FingerprintEnvironment.Current;
        var fingerprint = FingerprintBuilder.From(app, environment);
        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        var installer = InstallerInfoDetector.Detect(app, parsed);
        var command = InstallerInfoDetector.BuildCommand(app, installer, preferSilent: true);

        if (json)
        {
            var report = new InfoReport
            {
                App = AppReport.From(app),
                InstallerFamily = installer.Family.ToString().ToLowerInvariant(),
                SilentSupported = installer.SupportsSilent,
                SilentCommand = installer.SupportsSilent ? command.ToString() : null,
                NameTokens = fingerprint.NameTokens.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray(),
                VendorTokens = fingerprint.VendorTokens.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray(),
                InstallRoots = fingerprint.InstallRoots,
                RootCandidates = fingerprint.RootCandidates,
                LeftoverScanSupport = app.SupportsLeftoverScan ? "supported" : "not-applicable",
            };

            Console.Out.WriteLine(ReportJson.Serialize(report));
            return ExitCode.Ok;
        }

        ui.Header(app.DisplayName);
        ui.Line($"  Id:             {app.Id}");
        ui.Line($"  Version:        {app.Version ?? "-"}");
        ui.Line($"  Publisher:      {app.Publisher ?? "-"}");
        ui.Line($"  Source:         {app.SourceLabel} ({app.ScopeLabel})");
        ui.Line($"  Installed:      {app.InstallDate?.ToString("yyyy-MM-dd") ?? "-"}");
        ui.Line($"  Reported size:  {SizeFormat.Bytes(app.EstimatedSizeBytes)}");
        ui.Line($"  Install folder: {app.InstallLocation ?? "-"}");
        ui.Line($"  Uninstall:      {app.UninstallString ?? "-"}");
        ui.Line($"  Quiet uninstall:{app.QuietUninstallString ?? "-"}");
        ui.Line($"  Product code:   {app.ProductCode ?? "-"}");
        ui.Line($"  Registry key:   {app.RegistryKeyPath ?? "-"}");

        ui.Header("Installer");
        ui.Line($"  Technology:     {installer.FamilyLabel}");
        ui.Line($"  Silent run:     {(installer.SupportsSilent ? command.ToString() : "not supported")}");
        if (installer.Note is not null)
        {
            ui.Muted($"  Note:           {installer.Note}");
        }

        ui.Header("Fingerprint (what the leftover engine compares against)");
        ui.Line($"  Name tokens:    {Join(fingerprint.NameTokens)}");
        ui.Line($"  Vendor tokens:  {Join(fingerprint.VendorTokens)}");
        ui.Line($"  Install roots:  {Join(fingerprint.InstallRoots)}");
        ui.Line($"  Candidates:     {fingerprint.RootCandidates.Count} possible folder(s) under AppData/ProgramData/Program Files");

        foreach (var candidate in fingerprint.RootCandidates.Take(10))
        {
            var exists = Directory.Exists(candidate) ? "exists" : "-";
            ui.Muted($"      {candidate}  ({exists})");
        }

        return ExitCode.Ok;

        static string Join(IEnumerable<string> values)
        {
            var items = values.ToArray();
            return items.Length == 0 ? "-" : string.Join(", ", items);
        }
    }
}
