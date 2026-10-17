using System.Diagnostics;
using VibeUninstaller.Core.Models;

namespace VibeUninstaller.Windows.Execution;

public sealed record UninstallRequest
{
    public required InstallCommand Command { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Extra time to wait after the last tracked process exits.</summary>
    public TimeSpan DescendantGrace { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Runs an application's own uninstaller, waits for the complete process tree
/// and interprets the exit code the way Windows installers define it.
/// </summary>
public sealed class UninstallRunner
{
    public async Task<UninstallRunResult> RunAsync(
        UninstallRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var command = request.Command;

        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            return new UninstallRunResult
            {
                Status = UninstallRunStatus.StartFailed,
                ErrorMessage = "No uninstall command was found for this application.",
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var isProtocol = command.FileName.Contains("://", StringComparison.Ordinal);
        var needsElevationPrompt = command.RequiresElevation && !AdminCheck.IsElevated;
        var useShellExecute = isProtocol || needsElevationPrompt;

        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            UseShellExecute = useShellExecute,
        };

        if (needsElevationPrompt && !isProtocol)
        {
            startInfo.Verb = "runas";
        }

        if (!useShellExecute)
        {
            var directory = SafeDirectory(command.FileName);
            if (directory is not null)
            {
                startInfo.WorkingDirectory = directory;
            }
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            return new UninstallRunResult
            {
                Status = UninstallRunStatus.StartFailed,
                ErrorMessage = exception.Message,
                Duration = stopwatch.Elapsed,
                CommandLine = command.ToString(),
            };
        }

        if (process is null)
        {
            return new UninstallRunResult
            {
                Status = UninstallRunStatus.StartFailed,
                ErrorMessage = "The uninstaller process could not be started.",
                Duration = stopwatch.Elapsed,
                CommandLine = command.ToString(),
            };
        }

        using (process)
        {
            progress?.Report($"Started {Path.GetFileName(command.FileName)}");

            if (isProtocol)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                return new UninstallRunResult
                {
                    Status = UninstallRunStatus.Succeeded,
                    ExitCode = 0,
                    Duration = stopwatch.Elapsed,
                    CommandLine = command.ToString(),
                    ProcessCount = 1,
                    ErrorMessage = "Opened in the associated application. The removal is completed there.",
                };
            }

            var tracker = new ProcessTracker(process.Id);
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var timedOut = false;
            var canceled = false;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(request.PollInterval, cancellationToken).ConfigureAwait(false);

                    var alive = tracker.Poll();
                    foreach (var item in alive)
                    {
                        if (!string.IsNullOrEmpty(item.Name) && reported.Add(item.Name))
                        {
                            progress?.Report($"Running {item.Name} ({alive.Count} process{(alive.Count == 1 ? string.Empty : "es")})");
                        }
                    }

                    if (alive.Count == 0)
                    {
                        await Task.Delay(request.DescendantGrace, cancellationToken).ConfigureAwait(false);
                        alive = tracker.Poll();
                        if (alive.Count == 0)
                        {
                            break;
                        }
                    }

                    if (stopwatch.Elapsed > request.Timeout)
                    {
                        timedOut = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            if (timedOut || canceled)
            {
                await TerminateAsync(process, progress).ConfigureAwait(false);
                return new UninstallRunResult
                {
                    Status = canceled ? UninstallRunStatus.Canceled : UninstallRunStatus.TimedOut,
                    ErrorMessage = canceled
                        ? "Canceled by the user. The application may be left partially removed."
                        : $"The uninstaller did not finish within {request.Timeout.TotalMinutes:N0} minutes and was closed.",
                    Duration = stopwatch.Elapsed,
                    CommandLine = command.ToString(),
                    ProcessCount = tracker.ProcessCount,
                };
            }

            int? exitCode = null;
            try
            {
                if (process.WaitForExit(10_000))
                {
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception)
            {
                // The exit code is not available for shell-executed processes.
            }

            var (status, rebootRequired) = InterpretExitCode(exitCode);
            return new UninstallRunResult
            {
                Status = status,
                ExitCode = exitCode,
                RebootRequired = rebootRequired,
                ErrorMessage = errorMessageFor(exitCode),
                Duration = stopwatch.Elapsed,
                CommandLine = command.ToString(),
                ProcessCount = tracker.ProcessCount,
            };

            string? errorMessageFor(int? code) => code switch
            {
                1602 => "The uninstaller was canceled.",
                1603 => "The uninstaller reported a fatal error (1603).",
                1605 or 1614 => "The application is no longer registered; it was probably already removed.",
                null => null,
                _ when status == UninstallRunStatus.Failed => $"The uninstaller exited with code {code}.",
                _ => null,
            };
        }
    }

    public static (UninstallRunStatus Status, bool RebootRequired) InterpretExitCode(int? exitCode) => exitCode switch
    {
        null => (UninstallRunStatus.Succeeded, false),
        0 => (UninstallRunStatus.Succeeded, false),
        3010 or 1641 => (UninstallRunStatus.Succeeded, true),
        1605 or 1614 => (UninstallRunStatus.Succeeded, false),
        1602 => (UninstallRunStatus.Canceled, false),
        _ => (UninstallRunStatus.Failed, false),
    };

    private static async Task TerminateAsync(Process process, IProgress<string>? progress)
    {
        try
        {
            if (!process.HasExited)
            {
                progress?.Report("Asking the uninstaller to close...");
                process.CloseMainWindow();
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Ignore, fall through to the forced kill.
        }

        try
        {
            if (!process.HasExited)
            {
                progress?.Report("Stopping the uninstaller process tree...");
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Nothing else we can do.
        }
    }

    private static string? SafeDirectory(string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(fileName);
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
