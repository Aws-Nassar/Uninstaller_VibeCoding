namespace VibeUninstaller.Core.Models;

/// <summary>
/// How the reviewed leftovers are removed. The defaults are the safe ones:
/// registry keys are backed up to .reg files unless the caller opts out.
/// Named to stay clear of Windows.Management.Deployment.RemovalOptions.
/// </summary>
public sealed record LeftoverRemovalOptions
{
    /// <summary>
    /// Export every registry key to a .reg file before deleting it. Turning
    /// this off only affects the backup; files still go to the Recycle Bin.
    /// </summary>
    public bool BackupRegistryKeys { get; init; } = true;
}
