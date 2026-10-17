namespace VibeUninstaller.Core.Analysis;

/// <summary>
/// Well-known system locations. Everything here is derived from the BCL so the
/// value can be built and used by both the snapshot engine and the classifier.
/// </summary>
public sealed record FingerprintEnvironment
{
    public required string WindowsDirectory { get; init; }

    public required string ProgramFiles { get; init; }

    public required string ProgramFilesX86 { get; init; }

    public required string ProgramData { get; init; }

    public required string LocalAppData { get; init; }

    public required string LocalAppDataLow { get; init; }

    public required string RoamingAppData { get; init; }

    public required string UserProfile { get; init; }

    public required string UserDesktop { get; init; }

    public required string PublicDesktop { get; init; }

    public required string Public { get; init; }

    public required string Temp { get; init; }

    public required string SystemDrive { get; init; }

    public static FingerprintEnvironment Current { get; } = new()
    {
        WindowsDirectory = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
        ProgramFiles = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
        ProgramFilesX86 = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)),
        ProgramData = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
        LocalAppData = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
        LocalAppDataLow = PathTools.Normalize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow")),
        RoamingAppData = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
        UserProfile = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        UserDesktop = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
        PublicDesktop = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)),
        Public = PathTools.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)),
        Temp = PathTools.Normalize(Path.GetTempPath()),
        SystemDrive = PathTools.Normalize(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!),
    };

    /// <summary>Folders that are always captured by the file snapshot.</summary>
    public IReadOnlyList<string> SnapshotRoots() => new[]
    {
        ProgramFiles,
        ProgramFilesX86,
        ProgramData,
        LocalAppData,
        LocalAppDataLow,
        RoamingAppData,
        UserDesktop,
        PublicDesktop,
    };
}
