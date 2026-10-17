namespace VibeUninstaller.Core.Models;

/// <summary>An uninstall command resolved for execution.</summary>
public sealed record InstallCommand
{
    public required string FileName { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public bool RequiresElevation { get; init; }

    /// <summary>True when the command was produced by the silent switch engine.</summary>
    public bool IsSilent { get; init; }

    public override string ToString() =>
        string.IsNullOrEmpty(Arguments) ? FileName : $"{FileName} {Arguments}";
}

public enum UninstallRunStatus
{
    /// <summary>The uninstaller exited with a success code.</summary>
    Succeeded,

    /// <summary>The uninstaller exited with a failure code.</summary>
    Failed,

    /// <summary>The uninstaller took too long and was abandoned.</summary>
    TimedOut,

    /// <summary>The user canceled the operation.</summary>
    Canceled,

    /// <summary>The command could not be started at all.</summary>
    StartFailed,
}

/// <summary>Outcome of running an application's own uninstaller.</summary>
public sealed record UninstallRunResult
{
    public required UninstallRunStatus Status { get; init; }

    public int? ExitCode { get; init; }

    /// <summary>True when the uninstaller asked for a reboot (exit codes 1641 / 3010).</summary>
    public bool RebootRequired { get; init; }

    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public int ProcessCount { get; init; }

    public string? CommandLine { get; init; }

    public bool IsSuccess => Status == UninstallRunStatus.Succeeded;
}

public enum UninstallStage
{
    Preparing,
    Snapshotting,
    RunningUninstaller,
    WaitingForProcesses,
    Rescanning,
    Classifying,
    Removing,
    Completed,
    Failed,
}

/// <summary>Progress information streamed to the UI while an uninstall runs.</summary>
public sealed record UninstallProgress
{
    public required UninstallStage Stage { get; init; }

    public string? Detail { get; init; }

    public double? Percent { get; init; }
}

public sealed record UninstallOptions
{
    /// <summary>Capture before/after snapshots and classify leftovers.</summary>
    public bool ScanLeftovers { get; init; } = true;

    /// <summary>Prefer a silent command line when one is known for the installer family.</summary>
    public bool UseSilentUninstall { get; init; }

    /// <summary>Maximum time to let the uninstaller run.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>Everything that happened during one uninstall session.</summary>
public sealed record UninstallSessionResult
{
    public required InstalledApp App { get; init; }

    public required UninstallRunResult Run { get; init; }

    public LeftoverScanResult? Scan { get; init; }

    public AppFingerprint? Fingerprint { get; init; }
}
