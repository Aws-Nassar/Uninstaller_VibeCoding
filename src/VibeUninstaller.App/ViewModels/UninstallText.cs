using VibeUninstaller.Core.Models;

namespace VibeUninstaller.App.ViewModels;

/// <summary>Shared wording for uninstall progress and results.</summary>
public static class UninstallText
{
    public static string DescribeStage(UninstallStage stage) => stage switch
    {
        UninstallStage.Preparing => "Preparing...",
        UninstallStage.Snapshotting => "Capturing the system state (this takes a few seconds)...",
        UninstallStage.RunningUninstaller => "Running the uninstaller...",
        UninstallStage.WaitingForProcesses => "Waiting for the uninstaller...",
        UninstallStage.Rescanning => "Re-scanning the system...",
        UninstallStage.Classifying => "Scoring leftovers...",
        UninstallStage.Removing => "Removing...",
        UninstallStage.Completed => "Finished",
        _ => "Working...",
    };

    /// <summary>A short one-line outcome for the batch status list.</summary>
    public static string DescribeRun(UninstallRunResult run) => run.Status switch
    {
        UninstallRunStatus.Succeeded => run.RebootRequired ? "Uninstalled — reboot required" : "Uninstalled",
        UninstallRunStatus.Failed => "The uninstaller reported a failure",
        UninstallRunStatus.TimedOut => "The uninstaller timed out",
        UninstallRunStatus.Canceled => "Canceled",
        _ => "The uninstaller could not be started",
    };
}
