using System.Diagnostics;
using VibeUninstaller.Core.Analysis;
using VibeUninstaller.Core.Execution;
using VibeUninstaller.Core.Models;
using VibeUninstaller.Snapshot;
using VibeUninstaller.Windows.Providers;

namespace VibeUninstaller.Windows.Execution;

/// <summary>
/// Orchestrates a complete uninstall session: finger print the app, snapshot the
/// system, run the vendor uninstaller, rescan, classify leftovers.
/// </summary>
public sealed class UninstallService
{
    private readonly FingerprintEnvironment _environment = FingerprintEnvironment.Current;
    private readonly LeftoverRules _rules;
    private readonly UninstallRunner _runner = new();

    public UninstallService()
    {
        _rules = LeftoverRules.CreateDefault(_environment);
    }

    public LeftoverRules Rules => _rules;

    public FingerprintEnvironment Environment => _environment;

    public async Task<UninstallSessionResult> UninstallAsync(
        InstalledApp app,
        UninstallOptions options,
        IProgress<UninstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var relay = new ProgressRelay(progress);
        relay.Stage = UninstallStage.Preparing;
        relay.Report($"Preparing {app.DisplayName}");

        if (app.Source == AppSource.Appx)
        {
            return await RemoveStoreAppAsync(app, relay, cancellationToken).ConfigureAwait(false);
        }

        var parsed = CommandLineSplitter.Split(app.UninstallString)
            ?? CommandLineSplitter.Split(app.QuietUninstallString);
        var installerInfo = InstallerInfoDetector.Detect(app, parsed);
        var command = InstallerInfoDetector.BuildCommand(app, installerInfo, options.UseSilentUninstall);

        var fingerprint = FingerprintBuilder.From(app, _environment);
        var installRootFiles = CollectInstallRootFileNames(fingerprint.InstallRoots);
        if (installRootFiles.Count > 0)
        {
            var merged = new HashSet<string>(fingerprint.KnownFileNames, StringComparer.OrdinalIgnoreCase);
            merged.UnionWith(installRootFiles);
            fingerprint = fingerprint with { KnownFileNames = merged };
        }

        // Steam removals happen in the Steam client; scanning right away would
        // report the game files as leftovers while Steam is still working.
        var wantScan = options.ScanLeftovers
            && app.SupportsLeftoverScan
            && app.Source != AppSource.Steam;

        RegistrySnapshot? registryBefore = null;
        FileSnapshot? filesBefore = null;
        var snapshotRoots = BuildSnapshotRoots(fingerprint);

        if (wantScan)
        {
            relay.Stage = UninstallStage.Snapshotting;
            relay.Report("Capturing the registry state before the uninstall...");
            registryBefore = RegistrySnapshot.Capture(_rules, relay.AsText(), cancellationToken);
            relay.Report("Capturing the file system state before the uninstall...");
            filesBefore = FileSnapshot.Capture(snapshotRoots, _rules, relay.AsText(), cancellationToken);
        }

        relay.Stage = UninstallStage.RunningUninstaller;
        relay.Report($"Running: {command}");
        var run = await _runner
            .RunAsync(new UninstallRequest { Command = command, Timeout = options.Timeout }, relay.AsText(), cancellationToken)
            .ConfigureAwait(false);

        var scanPossible = run.Status is UninstallRunStatus.Succeeded or UninstallRunStatus.Failed;
        if (!wantScan || !scanPossible)
        {
            relay.Stage = UninstallStage.Completed;
            return new UninstallSessionResult { App = app, Run = run, Fingerprint = fingerprint };
        }

        relay.Stage = UninstallStage.Rescanning;
        relay.Report("Letting the system settle before the after-scan...");
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

        relay.Report("Capturing the registry state after the uninstall...");
        var registryAfter = RegistrySnapshot.Capture(_rules, relay.AsText(), cancellationToken);

        relay.Report("Capturing the file system state after the uninstall...");
        var filesAfter = FileSnapshot.Capture(snapshotRoots, _rules, relay.AsText(), cancellationToken);

        relay.Stage = UninstallStage.Classifying;
        relay.Report("Comparing snapshots and scoring leftovers...");
        var diff = SnapshotDiffer.Diff(registryBefore!, registryAfter, filesBefore!, filesAfter, cancellationToken);
        var classifier = new LeftoverClassifier(fingerprint, _rules, _environment);
        var scan = classifier.Classify(diff, cancellationToken);

        relay.Stage = UninstallStage.Completed;
        relay.Report($"Found {scan.Items.Count} leftover item{(scan.Items.Count == 1 ? string.Empty : "s")}");

        return new UninstallSessionResult
        {
            App = app,
            Run = run,
            Scan = scan,
            Fingerprint = fingerprint,
        };
    }

    private static async Task<UninstallSessionResult> RemoveStoreAppAsync(
        InstalledApp app,
        ProgressRelay relay,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        relay.Stage = UninstallStage.RunningUninstaller;

        if (string.IsNullOrWhiteSpace(app.PackageFullName))
        {
            relay.Stage = UninstallStage.Failed;
            relay.Report("The package identity is missing.");
            return new UninstallSessionResult
            {
                App = app,
                Run = new UninstallRunResult
                {
                    Status = UninstallRunStatus.StartFailed,
                    ErrorMessage = "The package identity is missing.",
                },
            };
        }

        relay.Report($"Removing package {app.DisplayName}...");
        var (success, error) = await AppxAppProvider.RemoveAsync(app.PackageFullName, cancellationToken).ConfigureAwait(false);

        relay.Stage = success ? UninstallStage.Completed : UninstallStage.Failed;
        relay.Report(success ? "Package removed." : $"Package removal failed: {error}");

        return new UninstallSessionResult
        {
            App = app,
            Run = new UninstallRunResult
            {
                Status = success ? UninstallRunStatus.Succeeded : UninstallRunStatus.Failed,
                ErrorMessage = error,
                Duration = stopwatch.Elapsed,
                CommandLine = $"RemovePackage {app.PackageFullName}",
                ExitCode = success ? 0 : null,
            },
        };
    }

    private IReadOnlyList<string> BuildSnapshotRoots(AppFingerprint fingerprint)
    {
        var roots = new List<string>(_environment.SnapshotRoots());
        foreach (var root in fingerprint.InstallRoots)
        {
            if (!roots.Any(existing => PathTools.IsUnder(root, existing, includeEqual: true)))
            {
                roots.Add(root);
            }
        }

        return roots;
    }

    private static IReadOnlyCollection<string> CollectInstallRootFileNames(IReadOnlyList<string> installRoots)
    {
        var names = new List<string>();
        foreach (var root in installRoots.Take(4))
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = PathTools.LastSegment(file);
                    if (name.Length > 0)
                    {
                        names.Add(name);
                    }

                    if (names.Count >= 1_000)
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable install folders are fine; the fingerprint just stays smaller.
            }
        }

        return names;
    }

    private sealed class ProgressRelay(IProgress<UninstallProgress>? target)
    {
        public UninstallStage Stage { get; set; } = UninstallStage.Preparing;

        public void Report(string? detail, double? percent = null) =>
            target?.Report(new UninstallProgress { Stage = Stage, Detail = detail, Percent = percent });

        public IProgress<string> AsText() => new TextProgress(this);

        private sealed class TextProgress(ProgressRelay owner) : IProgress<string>
        {
            public void Report(string value) => owner.Report(value);
        }
    }
}
